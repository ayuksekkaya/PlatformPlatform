import FederatedSideMenu from "@/federated-modules/sideMenu/FederatedSideMenu";
import { TopMenu } from "@/shared/components/topMenu";
import logoWrap from "@/shared/images/logo-wrap.svg";
import { UserRole, api } from "@/shared/lib/api/client";
import { t } from "@lingui/core/macro";
import { Trans } from "@lingui/react/macro";
import { AppLayout } from "@repo/ui/components/AppLayout";
import { Breadcrumb } from "@repo/ui/components/Breadcrumbs";
import { Button } from "@repo/ui/components/Button";
import { Form } from "@repo/ui/components/Form";
import { TextField } from "@repo/ui/components/TextField";
import { toastQueue } from "@repo/ui/components/Toast";
import { mutationSubmitter } from "@repo/ui/forms/mutationSubmitter";
import { createFileRoute } from "@tanstack/react-router";
import { Trash2 } from "lucide-react";
import { useEffect, useState } from "react";
import { Separator } from "react-aria-components";
import DeleteAccountConfirmation from "./-components/DeleteAccountConfirmation";

export const Route = createFileRoute("/admin/account/")({
  component: AccountSettings
});

export function AccountSettings() {
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const { data: tenant, isLoading: tenantLoading } = api.useQuery("get", "/api/account-management/tenants/current");
  const { data: currentUser, isLoading: userLoading } = api.useQuery("get", "/api/account-management/users/me");
  const updateCurrentTenantMutation = api.useMutation("put", "/api/account-management/tenants/current");

  const isOwner = currentUser?.role === UserRole.Owner;

  useEffect(() => {
    if (updateCurrentTenantMutation.isSuccess) {
      toastQueue.add({
        title: t`Success`,
        description: t`Account updated successfully`,
        variant: "success"
      });
    }
  }, [updateCurrentTenantMutation.isSuccess]);

  if (tenantLoading || userLoading) {
    return null;
  }

  return (
    <>
      <FederatedSideMenu currentSystem="account-management" />
      <AppLayout
        variant="center"
        topMenu={
          <TopMenu>
            <Breadcrumb href="/admin/account">
              <Trans>Account</Trans>
            </Breadcrumb>
            <Breadcrumb>
              <Trans>Settings</Trans>
            </Breadcrumb>
          </TopMenu>
        }
        title={t`Account settings`}
        subtitle={t`Manage your account here.`}
      >
        <Form
          onSubmit={isOwner ? mutationSubmitter(updateCurrentTenantMutation) : undefined}
          validationErrors={isOwner ? updateCurrentTenantMutation.error?.errors : undefined}
          validationBehavior="aria"
          className="flex flex-col gap-4"
        >
          <h2>
            <Trans>Account information</Trans>
          </h2>
          <Separator />

          <Trans>Logo</Trans>

          <img src={logoWrap} alt={t`Logo`} className="max-h-16 max-w-64" />
          <TextField
            isRequired={true}
            name="name"
            defaultValue={tenant?.name ?? ""}
            isDisabled={updateCurrentTenantMutation.isPending}
            isReadOnly={!isOwner}
            label={t`Account name`}
            description={!isOwner ? t`Only account owners can modify the account name` : undefined}
            validationBehavior="aria"
          />
          {isOwner && (
            <Button type="submit" className="mt-4" isDisabled={updateCurrentTenantMutation.isPending}>
              {updateCurrentTenantMutation.isPending ? <Trans>Saving...</Trans> : <Trans>Save changes</Trans>}
            </Button>
          )}
        </Form>

        {isOwner && (
          <div className="mt-6 flex flex-col gap-4">
            <h2>
              <Trans>Danger zone</Trans>
            </h2>
            <Separator />
            <div className="flex flex-col gap-4">
              <p>
                <Trans>Delete your account and all data. This action is irreversible—proceed with caution.</Trans>
              </p>

              <Button variant="destructive" onPress={() => setIsDeleteModalOpen(true)} className="w-fit">
                <Trash2 />
                <Trans>Delete account</Trans>
              </Button>
            </div>
          </div>
        )}
      </AppLayout>

      <DeleteAccountConfirmation isOpen={isDeleteModalOpen} onOpenChange={setIsDeleteModalOpen} />
    </>
  );
}
