global using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestInit
{
	public static Sandbox.TestAppSystem AppSystem;

	[AssemblyInitialize]
	public static void AssemblyInitialize()
	{
		AppSystem = new Sandbox.TestAppSystem();
		AppSystem.Init();
	}

	[AssemblyCleanup]
	public static void AssemblyCleanup()
	{
		AppSystem.Shutdown();
	}
}
