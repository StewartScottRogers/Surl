// Assembly-level MSTest configuration for the Conformance suite only (BL-1090).
//
// Every other test project links the repository root's MSTestSettings.cs, which runs one
// worker per processor. Conformance tests each start real processes (a published surl,
// upstream curl, nested dotnet builds) and loopback servers, so on a 32-processor machine
// 32 workers starve one another: SSH key exchange times out, libcurl RTSP scripts run for
// minutes and upstream cases miss their deadlines. Capping the workers keeps the same
// parallelism benefit without the starvation.
[assembly: Parallelize(Workers = 8, Scope = ExecutionScope.MethodLevel)]
