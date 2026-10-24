namespace ResourceManagementSystem.Utilities;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ApiKeyRequiredAttribute : Attribute
{
}
