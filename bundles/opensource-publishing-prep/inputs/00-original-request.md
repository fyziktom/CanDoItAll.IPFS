# Original Request

Captured on 2026-06-29 from the user request in this Codex thread.

```text
You are senior C# architect you must use [$candoitall-bundle-workflow](C:\Users\dell\.codex\skills\candoitall-bundle-workflow\SKILL.md) to solve this:
IMPORTANT: You are preparing bundle only. do not do implementation yet.

Main goal:
Preparation for publishing of the app

Architect notes:
- we need to do review and hardening and refactoring before publishing as opensource.
- you must identify all messy parts like too long files, mixing responsibilities, too large UI files not splitted to subcomponents and things like this. use xlsx to do detailed checklists and plan. For example NodeControl mixing lots of services together and responsibilities in general. I case of wanting of node without UI it would be difficult to do it. You must identify proper parts that might be isolated into own projects as drivers, helpers or some addon over Engine project to improve maintanibility of the code. This will be base also for nonUI version with CLI that we will do later.
- our UI is for desktop large screen only. do not waste time on tuning on small and medium screens.
- I need you to use [$analyzing-dotnet-performance](C:\Users\dell\.codex\skills\analyzing-dotnet-performance\SKILL.md) and [$optimizing-ef-core-queries](C:\Users\dell\.codex\skills\optimizing-ef-core-queries\SKILL.md) to analyze our implementation and fins possible troubles we have.
- in root of this repo add docker compose file that will start node with db together and db and data of files will be preserved after restart/rebuild of the docker.
```
