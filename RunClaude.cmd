@echo off
setlocal
pushd "%~dp0"
REM Always run the best available coding model. "opus" is a moving alias that Claude
REM Code resolves to the newest Opus release at launch (today: Opus 5.5), so this file
REM does not go stale when a newer model ships. Honour CLAUDE_MODEL if the caller set
REM one (e.g. hrdrClaudeNative.cmd forwards it into the herdr pane), which allows
REM pinning an exact id such as claude-opus-5-5 or claude-sonnet-5.
if not defined CLAUDE_MODEL set "CLAUDE_MODEL=opus"
call claude --model %CLAUDE_MODEL% --dangerously-skip-permissions --verbose
popd
endlocal
