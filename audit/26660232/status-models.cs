// Build with Mono.Cecil (bundled with ilspycmd). Inputs: sts2.dll, model inventory, status guide.
using Mono.Cecil;
using Mono.Cecil.Cil;

using var assembly = AssemblyDefinition.ReadAssembly(args[0]);
var module = assembly.MainModule;
var kinds = module.Types.Single(t => t.Name == "CardType").Fields.Where(f => f.HasConstant)
    .ToDictionary(f => Convert.ToInt32(f.Constant), f => f.Name);
var expected = File.ReadLines(args[2]).Skip(1).Select(l => l.Split('\t')[0].Replace("_", ""))
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
int Literal(Instruction i) => i.OpCode.Code switch
{
    >= Code.Ldc_I4_0 and <= Code.Ldc_I4_8 => (int)i.OpCode.Code - (int)Code.Ldc_I4_0,
    Code.Ldc_I4_S or Code.Ldc_I4 => Convert.ToInt32(i.Operand),
    _ => throw new InvalidDataException(i.ToString())
};
int count = 0;
foreach (string line in File.ReadLines(args[1]))
{
    var fields = line.Split('\t');
    if (fields[0] != "sts2.dll") continue;
    var t = module.GetType(fields[1]);
    count++;
    var getter = t.Methods.SingleOrDefault(m => m.Name == "get_Type");
    string kind;
    if (getter != null)
    {
        var ins = getter.Body.Instructions;
        // Only variable getter in this fingerprint; audited separately with ILSpy.
        if (t.Name == "MadScience" && ins.Any(i => i.Operand is MethodReference m && m.Name == "get_TinkerTimeType")) continue;
        if (ins.Count != 2 || ins[1].OpCode.Code != Code.Ret) throw new InvalidDataException(t.FullName);
        kind = kinds[Literal(ins[0])];
    }
    else
    {
        var ctor = t.Methods.Single(m => m.IsConstructor && !m.IsStatic);
        var call = ctor.Body.Instructions.Single(i => i.OpCode.Code == Code.Call &&
            i.Operand is MethodReference m && m.DeclaringType.Name == "CardModel" && m.Name == ".ctor");
        var parameters = ((MethodReference)call.Operand).Parameters;
        int index = parameters.ToList().FindIndex(p => p.ParameterType.Name == "CardType");
        if (index < 0) throw new InvalidDataException(t.FullName);
        // Trailing rarity, target and library flag each consume one literal.
        var instruction = call.Previous;
        for (int j = parameters.Count - 1; j > index; j--, instruction = instruction.Previous) Literal(instruction);
        kind = kinds[Literal(instruction)];
    }
    if (kind == "Status")
    {
        Console.WriteLine($"{t.FullName}\t{(getter == null ? "constructor" : "type-getter")}");
        if (!found.Add(t.Name)) throw new InvalidDataException(t.Name);
    }
}
if (count != 724 || found.Count != 20 || !found.SetEquals(expected))
    throw new InvalidDataException($"Coverage mismatch: {count} native models, {found.Count} statuses.");
