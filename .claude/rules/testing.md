---
paths:
  - "*.UnitTests/**"
---
# Testing rules

- Framework: MSTest, the test framework in the .NET SDK. Assertions: the built-in
  `Assert`. No third-party test, mocking, or assertion library — write a hand-rolled
  fake instead (see the root `CLAUDE.md`).
- `[TestClass]` on the class, `[TestMethod]` on each test. MSTest discovers by
  attribute, so a test method without one silently never runs.
- The MSTest analyzers are on and warnings are errors: take the fix the analyzer names
  (`Assert.IsEmpty` over `Assert.AreEqual(0, ...)`, and so on) rather than suppressing
  it. MSTest 4 removed `Assert.ThrowsException<T>`; it is `Assert.ThrowsExactly<T>` and
  `Assert.ThrowsExactlyAsync<T>` now, and they demand that exact type — a
  `TaskCanceledException` escaping where the test expects `OperationCanceledException`
  is a failure, so cancel with `CancellationToken.ThrowIfCancellationRequested()`. Assembly-level settings live in `MSTestSettings.cs` at the repository root, which
  every test project links; tests run method-level parallel, so share no state between
  them.
- Test project name: the production project with `.UnitTests` in place of
  `.UnitLibrary` (e.g. `Surl.Core.UnitLibrary` -> `Surl.Core.UnitTests`), in its own
  directory immediately under the repository root. Never under a `tests/` folder.
- One test class per production class: `<ClassName>Tests`.
- Test method names: `MethodName_Condition_ExpectedResult`.
- A test project's own implementation of `IContentFileSystem` starts with `UnitTest`
  (`UnitTestInMemoryContentFileSystem`, `UnitTestThrowingContentFileSystem`), and so
  does its file, so no test fake can be taken for the production
  `DiskContentFileSystem` (Stewart, 2026-09-29).
- Arrange / Act / Assert sections separated by a blank line.
- Tests touching the network, file system, or a database get `[TestCategory("Integration")]`,
  which the fast run excludes with `--filter "TestCategory!=Integration"`.
- Protocol tests must not need `Integration`: drive the protocol server through a hand-written fake
  `IConnection` replaying request bytes measured from pinned upstream curl - never bytes
  from the Curl port (ADR-0003). Needing a live client means the seam is in the wrong
  place.
- No `Thread.Sleep`; use fakes for time (`TimeProvider`).
- A bug fix starts with a failing test that reproduces it.
