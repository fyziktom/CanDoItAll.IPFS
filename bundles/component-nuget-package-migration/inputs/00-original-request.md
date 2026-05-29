# Original Request

User request on 2026-05-29:

> Use [$candoitall-bundle-workflow](C:\Users\dell\.codex\skills\candoitall-bundle-workflow\SKILL.md) to solve this:
> we moved candoitall blazor components like BaseLib and others into own repo. They are builded as nuget packages in C:\repositories\CanDoItAll\ExternalPackages
> use them from there. Remove old connection to baselib and other components projects. we need to use them as nuget packages. It will need to add output.css correctly, because it is part of those libs. You must validate that our economy apps that uses those components looks the same as before. take screenshots with playwright mcp before and after and assure it is correct.

## Raw Notes

| ID | Literal note | Closure target |
| --- | --- | --- |
| N001 | Use the CanDoItAll bundle workflow. | Bundle prepared, executed, and closed with gate/proof artifacts. |
| N002 | Components such as BaseLib moved into their own repo and are built as NuGet packages in `C:\repositories\CanDoItAll\ExternalPackages`. | Local NuGet source configured and component package IDs/versions used. |
| N003 | Remove old connection to BaseLib and other component projects. | No external `CanDoItAll.Components` project references remain in the migrated app. |
| N004 | Add `output.css` correctly because it is part of those libs. | App links/serves `_content/CanDoItAll.Components.BaseLib/css/output.css` from the package static web assets. |
| N005 | Validate economy apps using those components look the same as before. | Playwright MCP before/after screenshots and visual review for representative economy component-consuming apps. |
