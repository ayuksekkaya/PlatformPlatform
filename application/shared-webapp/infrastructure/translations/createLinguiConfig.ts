import type { LinguiConfig } from "@lingui/conf";
import { formatter } from "@lingui/format-po";
import i18nConfig from "./i18n.config.json";

export function createLinguiConfig(): LinguiConfig {
  return {
    locales: Object.keys(i18nConfig),
    sourceLocale: "en-US",
    catalogs: [
      {
        path: "<rootDir>/shared/translations/locale/{locale}",
        include: ["<rootDir>/**/*.{ts,tsx}"],
        exclude: ["**/node_modules/**", "**/dist", "**/*.d.ts", "**/*.test.*", "**/.*"]
      }
    ],
    format: formatter({ origins: false })
  };
}
