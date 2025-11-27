using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.Extensibility;

Assembly baseAssembly = typeof(VisualStudioExtensibility).Assembly;
var documentListenerTypes = baseAssembly.GetTypes()
	.Where(t => t.FullName != null && t.Name.Contains("Document"))
	.OrderBy(t => t.FullName)
	.ToList();

Console.WriteLine($"Document-related types in base assembly: {documentListenerTypes.Count}");
foreach (Type type in documentListenerTypes.Take(10))
{
	Console.WriteLine(type.FullName);
}

var listenerType = baseAssembly.GetTypes().FirstOrDefault(t => t.Name == "IDocumentEventsListener");
Console.WriteLine(listenerType == null ? "IDocumentEventsListener not found" : listenerType.FullName);

Assembly editorAssembly = Assembly.Load("Microsoft.VisualStudio.Extensibility.Editor");
Type? textDocumentType = editorAssembly.GetType("Microsoft.VisualStudio.Extensibility.Editor.Data.ITextDocument");
if (textDocumentType != null)
{
	Console.WriteLine("ITextDocument members:");
	foreach (var member in textDocumentType.GetMembers())
	{
		Console.WriteLine(member.MemberType + " " + member.Name);
	}
}
