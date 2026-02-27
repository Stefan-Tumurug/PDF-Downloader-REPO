using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Configures MSTest to run test methods in parallel.
/// 
/// Scope = MethodLevel:
/// - Each test method may execute concurrently with others.
/// - Test classes are not isolated from parallel execution.
/// 
/// Important:
/// - All tests must be thread-safe.
/// - Shared mutable static state must be avoided.
/// - Test doubles must not share global state.
/// 
/// This improves test performance while keeping isolation at method level.
/// </summary>
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]