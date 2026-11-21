// Compile as a disposable net9 console app. Read PE metadata only; never load a target DLL.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var assemblies = new Dictionary<string, List<(string Name, string Parent, bool Abstract)>>();
foreach (string path in args)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    if (!pe.HasMetadata) continue;
    var reader = pe.GetMetadataReader();
    string Name(StringHandle ns, StringHandle name) => reader.GetString(ns) + "." + reader.GetString(name);
    string Def(TypeDefinition d) => Name(d.Namespace, d.Name);
    string Ref(TypeReference t) => Name(t.Namespace, t.Name);
    var local = new List<(string Name, string Parent, bool Abstract)>();
    foreach (var h in reader.TypeDefinitions)
    {
        var t = reader.GetTypeDefinition(h);
        if (t.IsNested || t.BaseType.IsNil) continue;
        string parent = t.BaseType.Kind switch
        {
            HandleKind.TypeReference => Ref(reader.GetTypeReference((TypeReferenceHandle)t.BaseType)),
            HandleKind.TypeDefinition => Def(reader.GetTypeDefinition((TypeDefinitionHandle)t.BaseType)),
            _ => ""
        };
        local.Add((Def(t), parent, t.Attributes.HasFlag(TypeAttributes.Abstract)));
    }
    assemblies[path] = local;
}
var models = new HashSet<string> { "MegaCrit.Sts2.Core.Models.CardModel" };
bool changed;
do
{
    changed = false;
    foreach (var types in assemblies.Values)
        foreach (var (name, parent, _) in types)
            if (models.Contains(parent)) changed |= models.Add(name);
} while (changed);
foreach (var (path, types) in assemblies)
    foreach (var (name, _, isAbstract) in types)
    {
        if (!isAbstract && models.Contains(name)) Console.WriteLine(Path.GetFileName(path) + "\t" + name);
    }
