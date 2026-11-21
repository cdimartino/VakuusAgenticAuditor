using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Inspect shipped metadata and CIL operands only; never load the game assembly.
using var pe = new PEReader(File.OpenRead(args.Single()));
var md = pe.GetMetadataReader();
var types = md.TypeDefinitions.ToDictionary(TypeName);
var ops = typeof(OpCodes).GetFields().Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(o => (ushort)o.Value);

foreach (var (family, name) in new[] {
    ("room", "Rooms.RoomType"), ("map_point", "Map.MapPointType"),
    ("reward", "Rewards.RewardType"), ("mode", "Runs.GameMode")
})
{
    foreach (var field in md.GetTypeDefinition(types["MegaCrit.Sts2.Core." + name]).GetFields())
    {
        var f = md.GetFieldDefinition(field);
        if (f.Attributes.HasFlag(FieldAttributes.Literal))
            Console.WriteLine($"{family}\t{md.GetString(f.Name)}");
    }
}

// Review this method list against each get_AllPossibleOptions / GenerateInitialOptions
// before updating the exact assembly. Some pools use a non-generic materializer
// (look for Relic<T> or a concrete cast), not a literal option key.
foreach (var (id, type, methods) in new[] {
    ("DARV", "Darv", ".cctor get_AllPossibleOptions GenerateInitialOptions"),
    ("NEOW", "Neow", "get_PositiveOptions get_CurseOptions get_LavaRockOption get_NeowsTalismanOption get_NutritiousOysterOption get_PomanderOption get_SmallCapsuleOption get_StoneHumidifierOption GenerateInitialOptions"),
    ("NONUPEIPE", "Nonupeipe", "get_OptionPool get_BeautifulBraceletEventOption"),
    ("OROBAS", "Orobas", "get_OptionPool1 get_OptionPool2 get_OptionPool3 get_SeaGlassOptions get_PrismaticGemOption GenerateInitialOptions"),
    ("PAEL", "Pael", "get_OptionPool1 get_OptionPool2 get_OptionPool3 get_PaelsClawOption get_PaelsToothOption get_PaelsGrowthOption get_PaelsLegionOption"),
    ("TANX", "Tanx", "get_BaseOptionPool get_TriBoomerangOption"),
    ("TEZCATARA", "Tezcatara", "get_OptionPool1 get_OptionPool2 get_OptionPool3 get_NutritiousSoupOption"),
    ("VAKUU", "Vakuu", "get_Pool1 get_Pool2 get_Pool3"),
    ("DOLL_ROOM", "DollRoom", ".cctor"),
    ("FAKE_MERCHANT", "FakeMerchant", ".cctor"),
    ("FAKE_MERCHANT", "FakeMerchant/<FoulPotionThrown>d__23", "MoveNext"),
    ("HUNGRY_FOR_MUSHROOMS", "HungryForMushrooms", "GenerateInitialOptions"),
    ("MUSEUM_OF_TORTURE", "MuseumOfTorture", "CalculateVars"),
    ("TEA_MASTER", "TeaMaster", "get_CanonicalVars GenerateInitialOptions"),
    ("THERMAE_NONUPEIPE", "ThermaeNonupeipe", "CalculateVars"),
})
{
    var available = md.GetTypeDefinition(types["MegaCrit.Sts2.Core.Models.Events." + type]).GetMethods();
    foreach (var name in methods.Split(' '))
    {
        var method = available.Select(md.GetMethodDefinition).Single(m => md.GetString(m.Name) == name);
        foreach (var relic in RelicOperands(method).Distinct())
            Console.WriteLine($"{(id is "DARV" or "NEOW" or "NONUPEIPE" or "OROBAS" or "PAEL" or "TANX" or "TEZCATARA" or "VAKUU" ? "ancient_relic" : "event_relic")}\t{id}\t{relic}\t{name}");
    }
}

foreach (var (name, handle) in types.Where(t => t.Key.StartsWith("MegaCrit.Sts2.Core.Models.Modifiers.")))
{
    if (md.GetTypeDefinition(handle).GetMethods().Select(md.GetMethodDefinition)
        .Any(m => md.GetString(m.Name) == "GenerateNeowOption"))
        Console.WriteLine($"neow_modifier\t{name.Split('.')[^1]}");
}

foreach (var (name, handle) in types)
{
    if (name.Contains('/') || md.GetTypeDefinition(handle).Attributes.HasFlag(TypeAttributes.Abstract))
        continue;
    if (name.StartsWith("MegaCrit.Sts2.Core.Models.Encounters."))
        Console.WriteLine($"encounter_model\t{name.Split('.')[^1]}");
    if (name.StartsWith("MegaCrit.Sts2.Core.Models.Events."))
        Console.WriteLine($"event_model\t{name.Split('.')[^1]}");
}

string TypeName(TypeDefinitionHandle h)
{
    var t = md.GetTypeDefinition(h);
    return t.IsNested ? TypeName(t.GetDeclaringType()) + "/" + md.GetString(t.Name)
        : md.GetString(t.Namespace) + "." + md.GetString(t.Name);
}

IEnumerable<string> RelicOperands(MethodDefinition method)
{
    var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
    while (il.RemainingBytes > 0)
    {
        ushort op = il.ReadByte();
        if (op == 0xfe) op = (ushort)(0xfe00 | il.ReadByte());
        var operand = ops[op].OperandType;
        if (operand is OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
        {
            var h = MetadataTokens.EntityHandle(il.ReadInt32());
            if (h.Kind == HandleKind.MethodSpecification)
            {
                var b = md.GetBlobReader(md.GetMethodSpecification((MethodSpecificationHandle)h).Signature);
                if (b.ReadByte() != 0x0a || b.ReadCompressedInteger() != 1 || b.ReadByte() != 0x12) continue;
                h = b.ReadTypeHandle();
            }
            if (h.Kind == HandleKind.TypeDefinition)
            {
                const string prefix = "MegaCrit.Sts2.Core.Models.Relics.";
                var fullName = TypeName((TypeDefinitionHandle)h);
                if (fullName.StartsWith(prefix)) yield return fullName[prefix.Length..];
            }
            continue;
        }
        if (operand == OperandType.InlineSwitch)
        {
            int count = il.ReadInt32();
            il.Offset += checked(4 * count);
            continue;
        }
        il.Offset += operand switch
        {
            OperandType.InlineNone => 0,
            OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
            OperandType.InlineVar => 2,
            OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
                or OperandType.InlineSig or OperandType.InlineString or OperandType.ShortInlineR => 4,
            OperandType.InlineI8 or OperandType.InlineR => 8,
            _ => throw new InvalidDataException($"Unrecognized CIL operand kind {operand}")
        };
    }
}
