import fs from "node:fs";
import path from "node:path";
import type { RsbuildConfig, RsbuildPlugin } from "@rsbuild/core";
import { logger } from "@rsbuild/core";

const manifestFile = "remoteEntry.js";
const applicationRoot = path.resolve(process.cwd(), "..", "..");
const systemRoot = path.resolve(process.cwd(), "..");
const applicationPackageJson = path.join(applicationRoot, "package.json");
const moduleFederationTypesPath = path.join(applicationRoot, "shared-webapp", "build", "module-federation-types");

if (!fs.existsSync(applicationPackageJson)) {
  throw new Error(`Cannot find package.json in the application root: ${applicationRoot}`);
}

const { dependencies } = require(applicationPackageJson);

const SYSTEM_ID = getSystemId();

type ModuleFederationPluginOptions = {
  exposes?: Record<`./${string}`, `./${string}.tsx` | `./${string}.ts` | `./${string}`>;
  remotes?: Record<string, { port: number }>;
};

export function ModuleFederationPlugin({
  exposes = {},
  remotes = {}
}: ModuleFederationPluginOptions = {}): RsbuildPlugin {
  generateModuleFederationTypesFolder(SYSTEM_ID, exposes);
  return {
    name: "ModuleFederationPlugin",
    setup(api) {
      api.modifyRsbuildConfig((userConfig, { mergeRsbuildConfig }) => {
        const extraConfig: RsbuildConfig = {
          moduleFederation: {
            options: {
              name: snakeCase(SYSTEM_ID),

              exposes,
              remotes: getAllRemotes(SYSTEM_ID, remotes),
              filename: manifestFile,
              shared: {
                ...dependencies,
                react: {
                  singleton: true,
                  requiredVersion: dependencies.react
                },
                "react-dom": {
                  singleton: true,
                  requiredVersion: dependencies["react-dom"]
                }
              }
            }
          }
        };
        return mergeRsbuildConfig(userConfig, extraConfig);
      });
    }
  };
}

function getSystemId() {
  return path.relative(applicationRoot, systemRoot);
}

/**
 * Get all the system ids in the application root. This is used to determine the
 * module federation remotes. We also make sure that the system has a WebApp
 * and is formatted as snake_case for the module federation name.
 */
function getEverySystem() {
  return fs
    .readdirSync(applicationRoot)
    .filter((system) => fs.statSync(path.join(applicationRoot, system)).isDirectory())
    .filter((system) => fs.existsSync(path.join(applicationRoot, system, "WebApp")))
    .toSorted((a, b) => a.localeCompare(b));
}

function snakeCase(str: string) {
  return str.toLowerCase().replace(/-/g, "_");
}

function getAllRemotes(currentSystem: string, remotes: Record<string, { port: number }>) {
  const result: Record<string, string> = {};
  const everySystem = getEverySystem();
  for (const [system] of Object.entries(remotes)) {
    if (system === currentSystem) {
      throw new Error(`Cannot add self as remote: ${system}`);
    }
    if (!everySystem.includes(system)) {
      throw new Error(`Cannot find system: ${system}`);
    }

    result[system] = `${snakeCase(system)}@/${system}/${manifestFile}`;

    logger.info(`[Module Federation] Remote: "${system}" => ${result[system]}`);
  }
  return result;
}

function generateModuleFederationTypesFolder(system: string, exposes: Record<string, string> = {}) {
  const systemTypesDefinitionPath = getTypeDefinitionPath(system);
  if (Object.keys(exposes).length === 0) {
    if (fs.existsSync(systemTypesDefinitionPath)) {
      fs.unlinkSync(systemTypesDefinitionPath);
    }
    return;
  }
  const indexContent = Object.entries(exposes)
    .map(([exportPath, importPath]) => {
      logger.info(`[Module Federation] Expose: ${exportPath} => ${importPath}`);

      // Pattern matching for different module types
      // Translation files follow the pattern: ./translations/xx-XX (e.g., ./translations/en-US)
      const translationPattern = /^\.\/translations\/[a-z]{2}-[A-Z]{2}$/;
      if (translationPattern.test(exportPath)) {
        return `declare module "${exportPath.replace(/^\./, system)}" {\n  import type { Messages } from "@lingui/core";\n  export const messages: Messages;\n}`;
      }

      // Default to ReactNode export for components
      return `declare module "${exportPath.replace(/^\./, system)}" {\n  export default ReactNode;\n}`;
    })
    .join("\n");

  if (!fs.existsSync(moduleFederationTypesPath)) {
    fs.mkdirSync(moduleFederationTypesPath);
  }

  fs.writeFileSync(
    systemTypesDefinitionPath,
    `// This file was auto-generated by the ModuleFederationPlugin\n\n${indexContent.trim()}\n`,
    "utf-8"
  );
}

function getTypeDefinitionPath(system: string) {
  return path.join(moduleFederationTypesPath, `${system}.d.ts`);
}
