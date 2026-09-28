---
description: Search the Surl task board for text in a task's title or body.
argument-hint: <text to search for>
---
Search the board for: **$ARGUMENTS**

1. Run:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-admin.ps1 find -Text "$ARGUMENTS"`
   Add `-Folder`, `-Assignee`, `-Priority` or `-Pipeline` if the request narrows it.
2. The output gives each matching task and the numbered lines that matched. Relay the
   matches, and lead with the state each task is in — whether the thing being searched for
   is already in flight is usually the actual question.
3. If there are no matches, say so and do not paraphrase the search into something that
   would have matched. Suggest `/task-list All` if they want to see everything.

Searching is case-insensitive and matches a substring, so a short fragment is better than
a sentence.
