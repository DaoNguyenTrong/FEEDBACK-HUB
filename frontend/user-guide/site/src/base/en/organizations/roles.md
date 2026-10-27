---
title: Roles
description: "See the organization's roles, and create, edit or delete custom roles."
permission: organizations.roles.manage
---

# Roles

A role is a set of permissions given to members. Every organization comes with three roles, **Owner**, **Admin** and **Member**; you create your own roles when you need them.

## Before you start

- You have selected the right organization (see [switch organization](./switch)).

## See the roles

Click **Members** in the sidebar, then the **Roles** tab. Each row shows the role's name, its permissions and how many people hold it ("… people"). The **Owner** role reads "All permissions"; a role with no permission yet reads "No permissions".

A row tagged **View only** is a role you can't edit. Built-in roles can't be edited or deleted. A custom role in which you don't hold every permission is also view only: "You don't have every permission in this role, so you can only view it."

## Create a role

1. Click **Create role** at the top right (**Create** on a phone). The **Create role** panel opens.
2. Enter the **Role name** (up to 100 characters).
3. Under **Permissions**, tick the permissions this role has. You can only pick permissions you have.
4. Click **Create role**. Click **Cancel** to discard.

## Edit a role

1. On the role's row, click the three-dot button (**Actions for role** and its name), then choose **Edit role**.
2. Change the name or the permissions.
3. Click **Save**.

## Delete a role

1. On the role's row, click the three-dot button, then choose **Delete role**.
2. The app asks "Delete role …?". If invitations with this role are pending, the app says they will no longer work. Click **Delete role** to confirm.

## The permissions

| Permission                     | Allows                                                |
| ------------------------------ | ----------------------------------------------------- |
| **Manage organization**        | Rename and deactivate the organization.               |
| **Manage members**             | Invite and remove members and change their roles.     |
| **Manage roles**               | Create, edit and delete custom roles.                 |
| **Manage API keys**            | Create and revoke the organization's API keys.        |
| **View audit log**             | See who did what in the organization.                 |
| **Manage files**               | Manage the files uploaded to the organization.        |
| **Manage settings**            | Change the organization's general settings.           |
| **FeedbackHub administration** | Manage products and all feedback of the organization. |

## Result

The app says "Created role …", "Saved role …" or "Deleted role …", and the list updates.

## Common problems

- **"Enter a role name."**: the **Role name** box is empty.
- **"That name is already used in this organization."**: choose another name.
- **"That name belongs to a built-in role."**: choose a name different from **Owner**, **Admin** and **Member**.
- **Delete role is switched off and the row says "This role is still held by … people. Change their roles or remove them from the organization first."**: change those people's roles on the [Members](./members) page, or remove them, then delete again.
- **A permission is switched off in the list**: you don't have it, so you can't give it.
