import { createFileRoute } from "@tanstack/react-router";
import { Menu } from "./-components/Menu";
import { UserTabs } from "./-components/UserTabs";
import { UserQuerying } from "./-components/UserQuerying";
import { UserTable } from "./-components/UserTable";
import { UserInvite } from "./-components/UserInvite";
import { Suspense, useEffect, useState } from "react";
import { accountManagementApi } from "@/shared/lib/api/client";
import { z } from "zod";
import type { components } from "@/shared/lib/api/api.generated";
import { SharedSideMenu } from "@/shared/components/SharedSideMenu";

const userPageSearchSchema = z.object({
  pageOffset: z.number().optional().catch(0)
});

export const Route = createFileRoute("/admin/users/")({
  component: UsersPage,
  validateSearch: userPageSearchSchema
});

export default function UsersPage() {
  const [pageOffset, setPageOffset] = useState(0);
  const [orderBy, setOrderBy] = useState<components["schemas"]["SortableUserProperties"]>();
  const [sortOrder, setSortOrder] = useState<components["schemas"]["SortOrder"]>();
  const [userData, setUserData] = useState<components["schemas"]["GetUsersResponseDto"] | null>(null);

  useEffect(() => {
    accountManagementApi
      .GET("/api/account-management/users", {
        params: {
          query: {
            PageOffset: pageOffset,
            OrderBy: orderBy,
            SortOrder: sortOrder
          }
        }
      })
      .then(({ data }) => setUserData(data ?? null))
      .catch((e) => console.error(e));
  }, [pageOffset, orderBy, sortOrder]);

  return (
    <div className="flex gap-4 w-full h-full">
      <SharedSideMenu />
      <div className="flex flex-col gap-4 px-2 sm:px-4 py-2 md:py-4 w-full">
        <Menu />
        <UserInvite />
        <UserTabs usersData={userData} />
        <UserQuerying />
        <Suspense fallback={<div>Loading data...</div>}>
          <UserTable usersData={userData} onPageChange={setPageOffset} onSortChange={[setOrderBy, setSortOrder]} />
        </Suspense>
      </div>
    </div>
  );
}
