using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text.RegularExpressions;

if (args.Length != 2) throw new ArgumentException("Expected assembly and output JSON paths");
using var stream = File.OpenRead(args[0]);
using var pe = new PEReader(stream);
var reader = pe.GetMetadataReader();
var provider = new Names(reader);
var opcodes = typeof(OpCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => unchecked((ushort)o.Value));
var focus = new Regex(@"^(Verse\.Text|Verse\.TexButton|RimWorld\.TexButton|Verse\.TexCommand|RimWorld\.TexCommand|Verse\.TexUI|Verse\.TexMisc|Verse\.ContentFinder`1|Verse\.ContentLoader`1|RimWorld\.MainMenuDrawer)$");
var words = new Regex(@"plus|add|load|tutorial|scroll|cog|gear|setting|briefcase|book|credit|exit|quit|search|font|arial|roboto|inter|sans", RegexOptions.IgnoreCase);
var path = new Regex(@"^(UI/|Fonts/|Textures/|Materials/)", RegexOptions.IgnoreCase);
var matches = new List<object>();
var focused = new List<object>();
int methodCount = 0, parseFailures = 0, resourceLiteralCount = 0;
foreach (var typeHandle in reader.TypeDefinitions)
{
    var type = reader.GetTypeDefinition(typeHandle);
    string typeName = provider.Type(typeHandle);
    bool isFocus = focus.IsMatch(typeName);
    var focusMethods = new List<object>();
    foreach (var methodHandle in type.GetMethods())
    {
        var method = reader.GetMethodDefinition(methodHandle);
        if (method.RelativeVirtualAddress == 0) continue;
        methodCount++;
        string methodName = reader.GetString(method.Name);
        List<Instruction> instructions;
        try { instructions = Decode(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()); }
        catch (Exception ex) { parseFailures++; if (isFocus) focusMethods.Add(new { method = methodName, error = ex.GetType().Name }); continue; }
        if (isFocus && (methodName == ".cctor" || typeName == "Verse.Text" || typeName.StartsWith("Verse.Content")))
            focusMethods.Add(new { method = methodName, instructions });
        for (int i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];
            if (instruction.opcode != "ldstr") continue;
            string literal = instruction.operand;
            if (!path.IsMatch(literal)) continue;
            resourceLiteralCount++;
            if (!words.IsMatch(literal) && !isFocus) continue;
            string nearbyStore = null;
            for (int j = i+1; j < Math.Min(instructions.Count, i+10); j++)
            {
                if (instructions[j].opcode == "ldstr") break;
                if (instructions[j].opcode == "stsfld") { nearbyStore = instructions[j].operand; break; }
            }
            matches.Add(new { declaring_type = typeName, method = methodName, il_offset = instruction.offset,
                              literal, nearby_static_store = nearbyStore,
                              context = instructions.Skip(Math.Max(0,i-2)).Take(12).ToArray() });
        }
    }
    if (isFocus)
        focused.Add(new { type = typeName,
            fields = type.GetFields().Select(h => { var f = reader.GetFieldDefinition(h); return new { name = reader.GetString(f.Name), type = f.DecodeSignature(provider, (object)null), attributes = f.Attributes.ToString() }; }).ToArray(),
            methods = focusMethods });
}
var result = new { schema = "native-asset-metadata-audit-v1", assembly = Path.GetFullPath(args[0]),
    method_bodies_read = methodCount, parse_failures = parseFailures, resource_literals_seen = resourceLiteralCount,
    execution = "PE metadata and IL bytes only; target assembly never loaded or initialized",
    manifest_resources = reader.ManifestResources.Select(h => reader.GetString(reader.GetManifestResource(h).Name)).ToArray(),
    focused_types = focused, candidate_literals = matches };
File.WriteAllText(args[1], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Read {methodCount} method bodies; {matches.Count} candidate resource literals; parse failures {parseFailures}");

List<Instruction> Decode(byte[] il)
{
    var list = new List<Instruction>();
    int at = 0;
    while (at < il.Length)
    {
        int start = at;
        ushort code = il[at++];
        if (code == 0xfe) code = (ushort)(0xfe00 | il[at++]);
        if (!opcodes.TryGetValue(code, out var op)) throw new InvalidDataException("Unknown IL opcode");
        string value = "";
        switch (op.OperandType)
        {
            case OperandType.InlineNone: break;
            case OperandType.ShortInlineI: value = ((sbyte)il[at++]).ToString(); break;
            case OperandType.ShortInlineVar: value = il[at++].ToString(); break;
            case OperandType.InlineVar: value = BitConverter.ToUInt16(il,at).ToString(); at+=2; break;
            case OperandType.InlineI: value = BitConverter.ToInt32(il,at).ToString(); at+=4; break;
            case OperandType.InlineI8: value = BitConverter.ToInt64(il,at).ToString(); at+=8; break;
            case OperandType.ShortInlineR: value = BitConverter.ToSingle(il,at).ToString(System.Globalization.CultureInfo.InvariantCulture); at+=4; break;
            case OperandType.InlineR: value = BitConverter.ToDouble(il,at).ToString(System.Globalization.CultureInfo.InvariantCulture); at+=8; break;
            case OperandType.ShortInlineBrTarget: value = (at+1+(sbyte)il[at]).ToString("X4"); at++; break;
            case OperandType.InlineBrTarget: value = (at+4+BitConverter.ToInt32(il,at)).ToString("X4"); at+=4; break;
            case OperandType.InlineSwitch: int count=BitConverter.ToInt32(il,at); at+=4+count*4; value=$"{count} targets"; break;
            case OperandType.InlineString: int token=BitConverter.ToInt32(il,at); at+=4; value=reader.GetUserString(MetadataTokens.UserStringHandle(token & 0xffffff)); break;
            default: int handle=BitConverter.ToInt32(il,at); at+=4; value=provider.Entity(MetadataTokens.EntityHandle(handle)); break;
        }
        if (at > il.Length) throw new InvalidDataException("Truncated IL operand");
        list.Add(new Instruction(start.ToString("X4"),op.Name,value));
    }
    return list;
}
record Instruction(string offset,string opcode,string operand);

sealed class Names : ISignatureTypeProvider<string,object>
{
    readonly MetadataReader r;
    internal Names(MetadataReader reader) { r=reader; }
    internal string Type(TypeDefinitionHandle h) { var t=r.GetTypeDefinition(h); return Join(r.GetString(t.Namespace),r.GetString(t.Name)); }
    static string Join(string n,string s) => n.Length==0?s:n+"."+s;
    internal string Entity(EntityHandle h)
    {
        switch(h.Kind)
        {
            case HandleKind.TypeDefinition:return Type((TypeDefinitionHandle)h);
            case HandleKind.TypeReference:var t=r.GetTypeReference((TypeReferenceHandle)h);return Join(r.GetString(t.Namespace),r.GetString(t.Name));
            case HandleKind.TypeSpecification:return r.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(this,(object)null);
            case HandleKind.MethodDefinition:var m=r.GetMethodDefinition((MethodDefinitionHandle)h);return Type(m.GetDeclaringType())+"."+r.GetString(m.Name);
            case HandleKind.FieldDefinition:var f=r.GetFieldDefinition((FieldDefinitionHandle)h);return Type(f.GetDeclaringType())+"."+r.GetString(f.Name);
            case HandleKind.MemberReference:var a=r.GetMemberReference((MemberReferenceHandle)h);return Entity(a.Parent)+"."+r.GetString(a.Name);
            case HandleKind.MethodSpecification:return Entity(r.GetMethodSpecification((MethodSpecificationHandle)h).Method)+"<method-spec>";
            default:return h.Kind+":"+MetadataTokens.GetToken(h).ToString("X8");
        }
    }
    public string GetArrayType(string e,ArrayShape s)=>e+"["+new string(',',s.Rank-1)+"]";
    public string GetByReferenceType(string e)=>e+"&";
    public string GetFunctionPointerType(MethodSignature<string> s)=>"function-pointer";
    public string GetGenericInstantiation(string e,ImmutableArray<string> a)=>e+"<"+string.Join(",",a)+">";
    public string GetGenericMethodParameter(object c,int i)=>"!!"+i;
    public string GetGenericTypeParameter(object c,int i)=>"!"+i;
    public string GetModifiedType(string m,string e,bool required)=>e;
    public string GetPinnedType(string e)=>e+" pinned";
    public string GetPointerType(string e)=>e+"*";
    public string GetPrimitiveType(PrimitiveTypeCode c)=>c.ToString();
    public string GetSZArrayType(string e)=>e+"[]";
    public string GetTypeFromDefinition(MetadataReader reader,TypeDefinitionHandle h,byte k)=>Type(h);
    public string GetTypeFromReference(MetadataReader reader,TypeReferenceHandle h,byte k)=>Entity(h);
    public string GetTypeFromSpecification(MetadataReader reader,object context,TypeSpecificationHandle h,byte k)=>Entity(h);
}