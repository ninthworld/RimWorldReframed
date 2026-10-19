using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length != 3)
    throw new ArgumentException("Expected: repository-directory game-directory output-directory");
string repo = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
string gameAssembly = Path.Combine(game, "RimWorldWin64_Data/Managed/Assembly-CSharp.dll");
string installedAssembly = Path.Combine(game, "Mods/RimWorldReframed/Assemblies/ReframedUI.dll");
using var stream = File.OpenRead(gameAssembly);
using var pe = new PEReader(stream);
var reader = pe.GetMetadataReader();
var types = new Dictionary<string, NativeType>(StringComparer.Ordinal);
string TypeName(EntityHandle handle)
{
    if (handle.IsNil) return "";
    if (handle.Kind == HandleKind.TypeDefinition)
    {
        var t = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
        var parent = t.GetDeclaringType();
        string name = reader.GetString(t.Name);
        return parent.IsNil ? Join(reader.GetString(t.Namespace), name) : TypeName(parent) + "+" + name;
    }
    if (handle.Kind == HandleKind.TypeReference)
    {
        var t = reader.GetTypeReference((TypeReferenceHandle)handle);
        string name = reader.GetString(t.Name);
        return t.ResolutionScope.Kind == HandleKind.TypeReference
            ? TypeName(t.ResolutionScope) + "+" + name : Join(reader.GetString(t.Namespace), name);
    }
    if (handle.Kind == HandleKind.TypeSpecification)
    {
        var spec = reader.GetTypeSpecification((TypeSpecificationHandle)handle);
        var blob = reader.GetBlobReader(spec.Signature);
        if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance ||
            blob.ReadSignatureTypeCode() != SignatureTypeCode.TypeHandle)
            throw new InvalidDataException("Unsupported base type specification: " + MetadataTokens.GetToken(handle).ToString("X8"));
        // The generic definition identifies inheritance; arguments do not change UI family membership.
        return TypeName(blob.ReadTypeHandle());
    }
    throw new InvalidDataException("Unexpected type handle kind " + handle.Kind);
}
foreach (var handle in reader.TypeDefinitions)
{
    var t = reader.GetTypeDefinition(handle);
    string name = TypeName(handle);
    string[] methods = t.GetMethods().Select(h => reader.GetString(reader.GetMethodDefinition(h).Name))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    types.Add(name, new NativeType(name, TypeName(t.BaseType), (t.Attributes & TypeAttributes.Abstract) != 0,
        (t.Attributes & TypeAttributes.Interface) != 0, methods));
}
List<string> Chain(NativeType t)
{
    var result = new List<string>();
    var seen = new HashSet<string>(StringComparer.Ordinal) { t.Name };
    string parent = t.Base;
    while (parent.Length > 0)
    {
        if (!seen.Add(parent)) throw new InvalidDataException("Inheritance cycle at " + parent);
        result.Add(parent);
        if (!types.TryGetValue(parent, out var next)) break;
        parent = next.Base;
    }
    return result;
}
string[] familyRoots = ["Verse.Window", "RimWorld.Page", "RimWorld.MainTabWindow", "Verse.InspectTabBase", "RimWorld.ITab", "RimWorld.Planet.WITab"];
string[] helperRoots = ["Verse.Gizmo", "Verse.Designator", "RimWorld.PawnColumnWorker", "Verse.Letter", "Verse.ChoiceLetter", "RimWorld.ChoiceLetter", "RimWorld.ScenPart", "RimWorld.Planet.WorldObject", "Verse.Listing", "Verse.TipWindow"];
var helperNameRule = new Regex(@"(?:Widgets|CardUtility|UI$|UIUtility$|UIDrawer$|(?:^|\.)(?:StatsReportUtility|StatDrawEntry|TabDrawer|QuickSearchWidget|QuickSearchFilter|SearchWidget|FloatMenuOption|FloatMenuUtility|Text|GenUI)$)", RegexOptions.CultureInvariant);
// Candidate sweep only: neither GUI names nor helper inheritance prove a separately reachable screen.
bool IsUiMethod(string name) => name.Contains("OnGUI", StringComparison.Ordinal) ||
    name.EndsWith("GUI", StringComparison.Ordinal) || name is "DoWindowContents" or "FillTab" or
    "DrawInspectPane" or "DoInspectPaneButtons" or "DrawExtraSelectionOverlays" or
    "DoEditInterface" or "DrawExtraInfo" or "GizmoOnGUI" or "DrawOption";
var sourceFiles = Directory.EnumerateFiles(Path.Combine(repo, "Source"), "*.cs", SearchOption.AllDirectories)
    .Order(StringComparer.Ordinal).Select(path => new SourceFile(Relative(repo, path), File.ReadAllLines(path))).ToArray();
var rows = new List<InventoryRow>();
foreach (var t in types.Values.OrderBy(t => t.Name, StringComparer.Ordinal))
{
    if (t.Name == "<Module>") continue;
    var chain = Chain(t);
    bool IsFamily(string root) => t.Name == root || chain.Contains(root, StringComparer.Ordinal);
    var roots = familyRoots.Where(IsFamily).ToArray();
    var helpers = helperRoots.Where(IsFamily).ToArray();
    bool helperName = helperNameRule.IsMatch(t.Name);
    var ownUiMethods = t.Methods.Where(name => IsUiMethod(name) || (helperName &&
        (name.StartsWith("Draw", StringComparison.Ordinal) || name.StartsWith("Do", StringComparison.Ordinal) || name.StartsWith("Fill", StringComparison.Ordinal)))).ToArray();
    // Compiler generated closure bodies are implementation details; their named parent remains a candidate.
    bool generated = t.Name.Contains('<', StringComparison.Ordinal);
    if (roots.Length == 0 && helpers.Length == 0 && ((!helperName && ownUiMethods.Length == 0) || generated)) continue;
    string category = IsFamily("RimWorld.MainTabWindow") ? "main-tab-window" :
        IsFamily("RimWorld.Page") ? "page" : IsFamily("Verse.Window") ? "window" :
        IsFamily("RimWorld.ITab") ? "inspect-tab" : IsFamily("RimWorld.Planet.WITab") ? "world-inspect-tab" :
        IsFamily("Verse.InspectTabBase") ? "inspect-tab-base" : "ui-helper-candidate";
    var reasons = new List<string>();
    reasons.AddRange(roots.Select(r => "inheritance:" + r));
    reasons.AddRange(helpers.Select(r => "helper-inheritance:" + r));
    if (helperName) reasons.Add("helper-type-name-rule");
    if (ownUiMethods.Length > 0) reasons.Add("declared-ui-method-name");
    string shortName = t.Name[(Math.Max(t.Name.LastIndexOf('.'), t.Name.LastIndexOf('+')) + 1)..];
    int genericAt = shortName.IndexOf('`');
    if (genericAt >= 0) shortName = shortName[..genericAt];
    var mention = new Regex(@"(?<![A-Za-z0-9_])" + Regex.Escape(shortName) + @"(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
    var hits = sourceFiles.SelectMany(f => f.Lines.Select((line, i) => (line, i)).Where(v => mention.IsMatch(v.line))
        .Select(v => f.Path + ":" + (v.i + 1))).ToArray();
    rows.Add(new InventoryRow(t.Name, category, t.Abstract, t.Interface, familyRoots.Contains(t.Name), t.Base,
        string.Join(" > ", chain), string.Join(";", reasons), string.Join(";", ownUiMethods),
        hits.Length, string.Join(";", hits), hits.Length > 0 ? "text-mention-only-not-coverage" : "no-text-mention-not-proof-of-uncovered"));
}
Directory.CreateDirectory(output);
string csvPath = Path.Combine(output, "native-ui-inventory.csv");
var csv = new StringBuilder("native_type,category,is_abstract,is_interface,is_family_root,direct_base_definition,base_chain,selection_reason,declared_ui_method_names,mod_source_mention_count,mod_source_mentions,mention_interpretation\n");
foreach (var r in rows)
    csv.AppendLine(string.Join(",", new[] { r.Name, r.Category, Bool(r.Abstract), Bool(r.Interface), Bool(r.Root), r.Base, r.Chain, r.Reasons, r.UiMethods, r.MentionCount.ToString(System.Globalization.CultureInfo.InvariantCulture), r.Mentions, r.Interpretation }.Select(Csv)));
File.WriteAllText(csvPath, csv.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
var availableData = Directory.EnumerateDirectories(Path.Combine(game, "Data")).Order(StringComparer.Ordinal).Select(path =>
{
    string aboutPath = Path.Combine(path, "About/About.xml");
    var about = File.Exists(aboutPath) ? XDocument.Load(aboutPath).Root : null;
    return new { directory = "Data/" + Path.GetFileName(path), package_id = about?.Element("packageId")?.Value,
        supported_versions = about?.Element("supportedVersions")?.Elements("li").Select(e => e.Value).ToArray() ?? [],
        about_sha256 = File.Exists(aboutPath) ? Hash(aboutPath) : null };
}).ToArray();
var manifest = new
{
    schema = "native-ui-metadata-inventory-v1",
    game_version = File.ReadAllText(Path.Combine(game, "Version.txt")).Trim(),
    game_assembly = new { relative_to_game = "RimWorldWin64_Data/Managed/Assembly-CSharp.dll", sha256 = Hash(gameAssembly), assembly_version = reader.GetAssemblyDefinition().Version.ToString(), mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid).ToString() },
    installed_mod = new { relative_to_game = "Mods/RimWorldReframed/Assemblies/ReframedUI.dll", sha256 = File.Exists(installedAssembly) ? Hash(installedAssembly) : null, assembly_version = File.Exists(installedAssembly) ? AssemblyVersion(installedAssembly) : null },
    available_data_directories = availableData,
    native_type_definitions = types.Count,
    inventory_rows = rows.Count,
    category_counts = rows.GroupBy(r => r.Category).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new { category = g.Key, total = g.Count(), concrete_classes = g.Count(r => !r.Abstract && !r.Interface), abstract_classes = g.Count(r => r.Abstract && !r.Interface), interfaces = g.Count(r => r.Interface) }).ToArray(),
    overlapping_family_counts_including_root = familyRoots.Select(root => new { root, total = types.Values.Count(t => t.Name == root || Chain(t).Contains(root)), concrete = types.Values.Count(t => !t.Abstract && !t.Interface && (t.Name == root || Chain(t).Contains(root))), abstract_classes = types.Values.Count(t => t.Abstract && !t.Interface && (t.Name == root || Chain(t).Contains(root))) }).ToArray(),
    family_roots = familyRoots,
    helper_roots = helperRoots.Where(types.ContainsKey).ToArray(),
    helper_type_name_rule = helperNameRule.ToString(),
    declared_ui_method_rule = "Contains OnGUI, ends GUI, or exact DoWindowContents/FillTab/DrawInspectPane/DoInspectPaneButtons/DrawExtraSelectionOverlays/DoEditInterface/DrawExtraInfo/GizmoOnGUI/DrawOption. Helper-type-name-rule matches also retain Draw/Do/Fill-prefixed method names. Compiler-generated closure candidates omitted unless in a selected hierarchy.",
    source_input_hashes = sourceFiles.Select(f => new { path = f.Path, sha256 = Hash(Path.Combine(repo, f.Path)) }).ToArray(),
    generator_input_hashes = new[] { "Tools/UIInventory/Program.cs", "Tools/UIInventory/UIInventory.csproj" }.Select(path => new { path, sha256 = Hash(Path.Combine(repo, path)) }).ToArray(),
    csv_sha256 = Hash(csvPath),
    bounds = new[] {
        "Metadata declarations from the installed Assembly-CSharp.dll only; game code never loaded or executed; no implementation bodies exported.",
        "Family totals overlap and include abstract roots. Concrete declarations are candidates, not proof of reachability or one screen per type.",
        "Helper candidates are a documented name/inheritance sweep, not an exhaustive inventory of all composited or dynamically generated UI.",
        "Source mentions include comments, guards and incidental text. They establish neither registered patches nor redesign, animation, translucency or acceptance coverage.",
        "Game assembly includes DLC code irrespective of installed/owned/active DLC. Data/About.xml reports local availability only; absent data and compiled classes do not establish ownership or runtime activation.",
        "Third-party mod assemblies, active ModsConfig, saves and live game state are outside this extraction. Generic direct bases are represented by their generic definition; base_chain follows definition inheritance."
    }
};
File.WriteAllText(Path.Combine(output, "native-ui-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
Console.WriteLine($"Wrote {rows.Count} metadata rows ({rows.Count(r => r.Category != "ui-helper-candidate")} window/inspector hierarchy records, {rows.Count(r => r.Category == "ui-helper-candidate")} helper candidates). No reachability, coverage or live acceptance inferred.");

static string Join(string ns, string name) => ns.Length == 0 ? name : ns + "." + name;
static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
static string AssemblyVersion(string path)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    return pe.GetMetadataReader().GetAssemblyDefinition().Version.ToString();
}
static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
static string Bool(bool value) => value ? "true" : "false";
static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
record NativeType(string Name, string Base, bool Abstract, bool Interface, string[] Methods);
record SourceFile(string Path, string[] Lines);
record InventoryRow(string Name, string Category, bool Abstract, bool Interface, bool Root, string Base, string Chain, string Reasons, string UiMethods, int MentionCount, string Mentions, string Interpretation);
