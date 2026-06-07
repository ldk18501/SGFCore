using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using ExcelDataReader;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class ConfigExporterEditor : EditorWindow
{
    private const string SETTINGS_PATH = "Assets/SGFCore/Modules/Config/ConfigExportSettings.asset";

    private ConfigExportSettings _settings;
    private Vector2 _scrollPosition;

    [MenuItem("Tools/Framework/配置表一键导出")]
    public static void ShowWindow()
    {
        GetWindow<ConfigExporterEditor>("配置导出工具");
    }

    private void OnEnable()
    {
        _settings = LoadOrCreateSettings();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        GUILayout.Label("配置表转换设置", EditorStyles.boldLabel);
        _settings = (ConfigExportSettings)EditorGUILayout.ObjectField("Settings", _settings, typeof(ConfigExportSettings), false);

        if (_settings == null)
        {
            if (GUILayout.Button("创建默认配置"))
            {
                _settings = LoadOrCreateSettings();
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUI.BeginChangeCheck();
        _settings.excelFolder = EditorGUILayout.TextField("Excel 目录", _settings.excelFolder);
        _settings.generatedCodeFolder = EditorGUILayout.TextField("生成代码目录", _settings.generatedCodeFolder);
        _settings.extensionCodeFolder = EditorGUILayout.TextField("扩展代码目录", _settings.extensionCodeFolder);
        _settings.bytesFolder = EditorGUILayout.TextField("Bytes 输出目录", _settings.bytesFolder);
        _settings.namespaceName = EditorGUILayout.TextField("命名空间", _settings.namespaceName);
        _settings.keyFieldName = EditorGUILayout.TextField("主键字段名", _settings.keyFieldName);
        _settings.clientFlag = EditorGUILayout.TextField("客户端列标记", _settings.clientFlag);
        _settings.arraySeparator = EditorGUILayout.TextField("数组分隔符", _settings.arraySeparator);
        _settings.vectorSeparator = EditorGUILayout.TextField("Vector 分隔符", _settings.vectorSeparator);

        GUILayout.Space(8);
        GUILayout.Label("多语言表", EditorStyles.boldLabel);
        _settings.languageClassName = EditorGUILayout.TextField("语言表类名", _settings.languageClassName);
        _settings.languageSourcePrefix = EditorGUILayout.TextField("语言 Excel 前缀", _settings.languageSourcePrefix);
        _settings.languageBytesPrefix = EditorGUILayout.TextField("语言 Bytes 前缀", _settings.languageBytesPrefix);
        _settings.defaultLanguageSuffix = EditorGUILayout.TextField("默认语言后缀", _settings.defaultLanguageSuffix);

        GUILayout.Space(8);
        GUILayout.Label("Addressables", EditorStyles.boldLabel);
        _settings.configureAddressables = EditorGUILayout.Toggle("自动设置 Addressables", _settings.configureAddressables);
        _settings.addressablesGroupName = EditorGUILayout.TextField("Addressables Group", _settings.addressablesGroupName);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
        }

        GUILayout.Space(12);
        if (GUILayout.Button("开始一键导出 (代码 + 数据)", GUILayout.Height(40)))
        {
            ProcessAllConfigs();
        }

        EditorGUILayout.EndScrollView();
    }

    private void ProcessAllConfigs()
    {
        try
        {
            EnsureFolder(_settings.generatedCodeFolder);
            EnsureFolder(_settings.extensionCodeFolder);
            EnsureFolder(_settings.bytesFolder);

            List<SheetModel> normalSheets = new List<SheetModel>();
            List<SheetModel> languageSheets = new List<SheetModel>();
            foreach (string file in FindExcelFiles(_settings.excelFolder))
            {
                SheetModel sheet = ParseExcel(file);
                if (IsLanguageSheet(sheet.SourceName))
                {
                    languageSheets.Add(sheet);
                }
                else
                {
                    normalSheets.Add(sheet);
                }
            }

            foreach (SheetModel sheet in normalSheets)
            {
                ExportNormalSheet(sheet);
            }

            if (languageSheets.Count > 0)
            {
                ExportLanguageSheets(languageSheets);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[Config] 导出完成。普通表: {normalSheets.Count}, 语言表: {languageSheets.Count}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Config] 导出失败: {e.Message}\n{e.StackTrace}");
        }
    }

    private IEnumerable<string> FindExcelFiles(string excelFolder)
    {
        if (string.IsNullOrWhiteSpace(excelFolder) || !Directory.Exists(excelFolder))
        {
            throw new DirectoryNotFoundException($"找不到 Excel 目录: {excelFolder}");
        }

        string[] files = Directory.GetFiles(excelFolder, "*.xlsx", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string normalized = NormalizePath(file);
            if (Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal) ||
                normalized.IndexOf("/Ignores/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            yield return file;
        }
    }

    private SheetModel ParseExcel(string path)
    {
        using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream))
        {
            DataSet result = reader.AsDataSet();
            if (result.Tables.Count == 0)
            {
                throw new InvalidDataException($"{path} 没有 Sheet。");
            }

            DataTable table = result.Tables[0];
            if (table.Rows.Count < 4)
            {
                throw new InvalidDataException($"{path} 至少需要 4 行表头：描述/标记/类型/字段名。");
            }

            SheetModel model = new SheetModel(path, Path.GetFileNameWithoutExtension(path));
            string clientFlag = NormalizeCell(_settings.clientFlag).ToUpperInvariant();
            for (int col = 0; col < table.Columns.Count; col++)
            {
                string flag = NormalizeCell(table.Rows[1][col]).ToUpperInvariant();
                if (flag != clientFlag)
                {
                    continue;
                }

                ConfigColumn column = new ConfigColumn(
                    col,
                    NormalizeCell(table.Rows[0][col]),
                    NormalizeCell(table.Rows[2][col]),
                    ToFieldName(NormalizeCell(table.Rows[3][col])));

                ValidateColumn(model, column);
                model.Columns.Add(column);
            }

            if (model.Columns.Count == 0)
            {
                throw new InvalidDataException($"{path} 没有任何客户端导出列，检查第 2 行标记是否为 {_settings.clientFlag}。");
            }

            model.KeyColumn = FindKeyColumn(model);
            ValidateKeyColumn(model);
            HashSet<string> keys = new HashSet<string>();
            for (int row = 4; row < table.Rows.Count; row++)
            {
                ConfigRow configRow = new ConfigRow(row + 1);
                bool hasValue = false;
                for (int i = 0; i < model.Columns.Count; i++)
                {
                    ConfigColumn column = model.Columns[i];
                    string value = NormalizeCell(table.Rows[row][column.SourceIndex]);
                    if (!string.IsNullOrEmpty(value))
                    {
                        hasValue = true;
                    }

                    configRow.Values[column.FieldName] = value;
                }

                if (!hasValue)
                {
                    continue;
                }

                string rawKey = configRow.Values[model.KeyColumn.FieldName];
                if (string.IsNullOrWhiteSpace(rawKey))
                {
                    throw new InvalidDataException($"{path} 第 {row + 1} 行主键为空。");
                }

                string key = NormalizeKey(rawKey, model.KeyColumn.TypeName, path, row + 1, model.KeyColumn.FieldName);
                if (!keys.Add(key))
                {
                    throw new InvalidDataException($"{path} 第 {row + 1} 行主键重复: {rawKey}");
                }

                model.Rows.Add(configRow);
            }

            return model;
        }
    }

    private void ExportNormalSheet(SheetModel sheet)
    {
        string className = GetNormalClassName(sheet.SourceName);
        GenerateCode(className, sheet);
        GenerateExtensionCode(className);

        string bytesPath = NormalizePath(Path.Combine(_settings.bytesFolder, className + ".bytes"));
        ExportBinary(bytesPath, sheet);
        ConfigureAddressable(bytesPath, className);
        Debug.Log($"[Config] 导出普通表: {className}");
    }

    private void ExportLanguageSheets(List<SheetModel> languageSheets)
    {
        languageSheets.Sort((a, b) => string.Compare(a.SourceName, b.SourceName, StringComparison.OrdinalIgnoreCase));
        SheetModel baseSheet = languageSheets[0];
        ValidateLanguageSheets(baseSheet, languageSheets);
        GenerateCode(_settings.languageClassName, baseSheet);
        GenerateExtensionCode(_settings.languageClassName);

        for (int i = 0; i < languageSheets.Count; i++)
        {
            SheetModel sheet = languageSheets[i];
            string suffix = GetLanguageSuffix(sheet.SourceName);
            string address = $"{_settings.languageBytesPrefix}_{suffix}";
            string bytesPath = NormalizePath(Path.Combine(_settings.bytesFolder, address + ".bytes"));
            ExportBinary(bytesPath, sheet);
            ConfigureAddressable(bytesPath, address);
            Debug.Log($"[Config] 导出多语言表数据: {address}");
        }
    }

    private void ValidateLanguageSheets(SheetModel baseSheet, List<SheetModel> languageSheets)
    {
        for (int i = 1; i < languageSheets.Count; i++)
        {
            SheetModel sheet = languageSheets[i];
            if (!HasSameSchema(baseSheet, sheet))
            {
                throw new InvalidDataException($"多语言表结构不一致: {baseSheet.SourceName} vs {sheet.SourceName}");
            }

            if (baseSheet.Rows.Count != sheet.Rows.Count)
            {
                throw new InvalidDataException($"多语言表行数不一致: {baseSheet.SourceName} vs {sheet.SourceName}");
            }

            for (int row = 0; row < baseSheet.Rows.Count; row++)
            {
                string baseKey = baseSheet.Rows[row].Values[baseSheet.KeyColumn.FieldName];
                string key = sheet.Rows[row].Values[sheet.KeyColumn.FieldName];
                if (baseKey != key)
                {
                    throw new InvalidDataException($"多语言表 key 不一致: {sheet.SourceName} 第 {sheet.Rows[row].ExcelRowIndex} 行，期望 {baseKey}，实际 {key}");
                }

                for (int col = 0; col < baseSheet.Columns.Count; col++)
                {
                    string fieldName = baseSheet.Columns[col].FieldName;
                    if (string.Equals(fieldName, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string baseValue = baseSheet.Rows[row].Values[fieldName];
                    string value = sheet.Rows[row].Values[fieldName];
                    if (baseValue != value)
                    {
                        throw new InvalidDataException($"多语言表非 value 字段不一致: {sheet.SourceName} 第 {sheet.Rows[row].ExcelRowIndex} 行字段 {fieldName}");
                    }
                }
            }
        }
    }

    private void GenerateCode(string className, SheetModel sheet)
    {
        string keyCsType = ToCsType(sheet.KeyColumn.TypeName);
        StringBuilder fields = new StringBuilder();
        StringBuilder readLogic = new StringBuilder();

        for (int i = 0; i < sheet.Columns.Count; i++)
        {
            ConfigColumn column = sheet.Columns[i];
            string csType = ToCsType(column.TypeName);
            fields.AppendLine($"    /// <summary> {EscapeSummary(column.Description)} </summary>");
            fields.AppendLine($"    public {csType} {column.FieldName};");
            AppendReadLogic(readLogic, column);
        }

        string namespaceOpen = string.IsNullOrWhiteSpace(_settings.namespaceName)
            ? string.Empty
            : $"namespace {_settings.namespaceName}\n{{\n";
        string namespaceClose = string.IsNullOrWhiteSpace(_settings.namespaceName) ? string.Empty : "}\n";
        string indent = string.IsNullOrWhiteSpace(_settings.namespaceName) ? string.Empty : "    ";
        string body = $@"// ------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具自动生成，请勿手动修改。
// </auto-generated>
// ------------------------------------------------------------------------------
using System.IO;
using UnityEngine;

{namespaceOpen}{indent}public partial class {className} : ConfigManagerBase<{keyCsType}, {className}>
{indent}{{
{Indent(fields.ToString(), indent)}
{indent}    public static void Load(byte[] data)
{indent}    {{
{indent}        Clear();
{indent}        ConfigBinaryCodec.Decode(data);

{indent}        using (MemoryStream ms = new MemoryStream(data))
{indent}        using (BinaryReader br = new BinaryReader(ms))
{indent}        {{
{indent}            int count = br.ReadInt32();
{indent}            for (int i = 0; i < count; i++)
{indent}            {{
{indent}                {className} item = new {className}();
{Indent(readLogic.ToString(), indent)}
{indent}                item.OnPostLoad();
{indent}                AddItem(item);
{indent}                AddIndex(item.{sheet.KeyColumn.FieldName}, item);
{indent}            }}
{indent}        }}

{indent}        OnAllLoadDone();
{indent}    }}

{indent}    private static string ReadString(BinaryReader br)
{indent}    {{
{indent}        int length = br.ReadInt32();
{indent}        return System.Text.Encoding.UTF8.GetString(br.ReadBytes(length));
{indent}    }}

{indent}    partial void OnPostLoad();
{indent}    static partial void OnAllLoadDone();
{indent}}}
{namespaceClose}";

        string path = NormalizePath(Path.Combine(_settings.generatedCodeFolder, className + "ConfigGenerated.cs"));
        File.WriteAllText(path, body, Encoding.UTF8);
    }

    private void AppendReadLogic(StringBuilder builder, ConfigColumn column)
    {
        string field = column.FieldName;
        string type = column.TypeName;
        string csType = ToCsType(type);

        if (IsEnum(type))
        {
            builder.AppendLine($"                item.{field} = ({csType})br.ReadInt32();");
            return;
        }

        if (IsArray(type))
        {
            string elementType = type.Substring(0, type.Length - 2);
            string elementCsType = ToCsType(elementType);
            builder.AppendLine($"                int {field}Count = br.ReadInt32();");
            builder.AppendLine($"                item.{field} = new {elementCsType}[{field}Count];");
            builder.AppendLine($"                for (int {field}Index = 0; {field}Index < {field}Count; {field}Index++)");
            builder.AppendLine("                {");
            builder.AppendLine($"                    item.{field}[{field}Index] = {ReadValueExpression(elementType)};");
            builder.AppendLine("                }");
            return;
        }

        builder.AppendLine($"                item.{field} = {ReadValueExpression(type)};");
    }

    private string ReadValueExpression(string type)
    {
        if (IsEnum(type))
        {
            return $"({ToCsType(type)})br.ReadInt32()";
        }

        switch (type)
        {
            case "int": return "br.ReadInt32()";
            case "long": return "br.ReadInt64()";
            case "float": return "br.ReadSingle()";
            case "double": return "br.ReadDouble()";
            case "bool": return "br.ReadBoolean()";
            case "string":
                return $"ReadString(br)";
            case "Vector2": return "new Vector2(br.ReadSingle(), br.ReadSingle())";
            case "Vector3": return "new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle())";
            case "Vector2Int": return "new Vector2Int(br.ReadInt32(), br.ReadInt32())";
            case "Vector3Int": return "new Vector3Int(br.ReadInt32(), br.ReadInt32(), br.ReadInt32())";
            default:
                throw new NotSupportedException($"不支持的字段类型: {type}");
        }
    }

    private void GenerateExtensionCode(string className)
    {
        string path = NormalizePath(Path.Combine(_settings.extensionCodeFolder, className + "ConfigExt.cs"));
        if (File.Exists(path))
        {
            return;
        }

        string namespaceOpen = string.IsNullOrWhiteSpace(_settings.namespaceName)
            ? string.Empty
            : $"namespace {_settings.namespaceName}\n{{\n";
        string namespaceClose = string.IsNullOrWhiteSpace(_settings.namespaceName) ? string.Empty : "}\n";
        string indent = string.IsNullOrWhiteSpace(_settings.namespaceName) ? string.Empty : "    ";
        string template = $@"{namespaceOpen}{indent}public partial class {className}
{indent}{{
{indent}    partial void OnPostLoad()
{indent}    {{
{indent}    }}

{indent}    static partial void OnAllLoadDone()
{indent}    {{
{indent}    }}
{indent}}}
{namespaceClose}";

        File.WriteAllText(path, template, Encoding.UTF8);
    }

    private void ExportBinary(string bytesPath, SheetModel sheet)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            bw.Write(sheet.Rows.Count);
            for (int row = 0; row < sheet.Rows.Count; row++)
            {
                for (int col = 0; col < sheet.Columns.Count; col++)
                {
                    ConfigColumn column = sheet.Columns[col];
                    string value = sheet.Rows[row].Values[column.FieldName];
                    WriteValue(bw, column.TypeName, value, sheet.SourcePath, sheet.Rows[row].ExcelRowIndex, column.FieldName);
                }
            }

            byte[] raw = ms.ToArray();
            ConfigBinaryCodec.Encode(raw);
            File.WriteAllBytes(bytesPath, raw);
        }
    }

    private void WriteValue(BinaryWriter writer, string type, string value, string sourcePath, int row, string fieldName)
    {
        if (IsEnum(type))
        {
            writer.Write(ParseInt(value, sourcePath, row, fieldName));
            return;
        }

        if (IsArray(type))
        {
            string elementType = type.Substring(0, type.Length - 2);
            string[] parts = SplitArray(value);
            writer.Write(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                WriteValue(writer, elementType, parts[i], sourcePath, row, fieldName);
            }
            return;
        }

        switch (type)
        {
            case "int": writer.Write(ParseInt(value, sourcePath, row, fieldName)); break;
            case "long": writer.Write(ParseLong(value, sourcePath, row, fieldName)); break;
            case "float": writer.Write(ParseFloat(value, sourcePath, row, fieldName)); break;
            case "double": writer.Write(ParseDouble(value, sourcePath, row, fieldName)); break;
            case "bool": writer.Write(ParseBool(value)); break;
            case "string": WriteString(writer, value ?? string.Empty); break;
            case "Vector2": WriteFloatVector(writer, value, 2, sourcePath, row, fieldName); break;
            case "Vector3": WriteFloatVector(writer, value, 3, sourcePath, row, fieldName); break;
            case "Vector2Int": WriteIntVector(writer, value, 2, sourcePath, row, fieldName); break;
            case "Vector3Int": WriteIntVector(writer, value, 3, sourcePath, row, fieldName); break;
            default: throw new NotSupportedException($"不支持的字段类型: {type}");
        }
    }

    private void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private void WriteFloatVector(BinaryWriter writer, string value, int count, string sourcePath, int row, string fieldName)
    {
        string[] parts = SplitVector(value);
        if (parts.Length != count)
        {
            throw new FormatException($"{sourcePath} 第 {row} 行字段 {fieldName} 需要 {count} 个 float。");
        }

        for (int i = 0; i < count; i++)
        {
            writer.Write(ParseFloat(parts[i], sourcePath, row, fieldName));
        }
    }

    private void WriteIntVector(BinaryWriter writer, string value, int count, string sourcePath, int row, string fieldName)
    {
        string[] parts = SplitVector(value);
        if (parts.Length != count)
        {
            throw new FormatException($"{sourcePath} 第 {row} 行字段 {fieldName} 需要 {count} 个 int。");
        }

        for (int i = 0; i < count; i++)
        {
            writer.Write(ParseInt(parts[i], sourcePath, row, fieldName));
        }
    }

    private void ConfigureAddressable(string assetPath, string address)
    {
        if (!_settings.configureAddressables)
        {
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            guid = AssetDatabase.AssetPathToGUID(assetPath);
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        }

        if (settings == null)
        {
            Debug.LogWarning("[Config] 找不到 Addressables Settings，已跳过自动配置。");
            return;
        }

        AddressableAssetGroup group = settings.FindGroup(_settings.addressablesGroupName);
        if (group == null)
        {
            group = settings.CreateGroup(
                _settings.addressablesGroupName,
                false,
                false,
                true,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
    }

    private ConfigColumn FindKeyColumn(SheetModel model)
    {
        for (int i = 0; i < model.Columns.Count; i++)
        {
            if (string.Equals(model.Columns[i].FieldName, _settings.keyFieldName, StringComparison.OrdinalIgnoreCase))
            {
                return model.Columns[i];
            }
        }

        return model.Columns[0];
    }

    private void ValidateKeyColumn(SheetModel model)
    {
        string type = model.KeyColumn.TypeName;
        if (IsEnum(type) || type == "int" || type == "long" || type == "string")
        {
            return;
        }

        throw new InvalidDataException($"{model.SourcePath} 主键字段 {model.KeyColumn.FieldName} 类型不适合作为配置索引: {type}");
    }

    private void ValidateColumn(SheetModel model, ConfigColumn column)
    {
        if (string.IsNullOrWhiteSpace(column.FieldName))
        {
            throw new InvalidDataException($"{model.SourcePath} 第 {column.SourceIndex + 1} 列字段名为空。");
        }

        if (!IsValidIdentifier(column.FieldName))
        {
            throw new InvalidDataException($"{model.SourcePath} 字段名不是合法 C# 标识符: {column.FieldName}");
        }

        if (!IsSupportedType(column.TypeName))
        {
            throw new InvalidDataException($"{model.SourcePath} 字段 {column.FieldName} 类型不支持: {column.TypeName}");
        }

        for (int i = 0; i < model.Columns.Count; i++)
        {
            if (model.Columns[i].FieldName == column.FieldName)
            {
                throw new InvalidDataException($"{model.SourcePath} 字段重复: {column.FieldName}");
            }
        }
    }

    private bool HasSameSchema(SheetModel a, SheetModel b)
    {
        if (a.Columns.Count != b.Columns.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Columns.Count; i++)
        {
            if (a.Columns[i].FieldName != b.Columns[i].FieldName ||
                a.Columns[i].TypeName != b.Columns[i].TypeName)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsLanguageSheet(string sourceName)
    {
        return sourceName.StartsWith(_settings.languageSourcePrefix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sourceName, _settings.languageClassName, StringComparison.OrdinalIgnoreCase);
    }

    private string GetLanguageSuffix(string sourceName)
    {
        if (sourceName.StartsWith(_settings.languageSourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return sourceName.Substring(_settings.languageSourcePrefix.Length);
        }

        return _settings.defaultLanguageSuffix;
    }

    private string GetNormalClassName(string sourceName)
    {
        string safeName = ToClassName(sourceName);
        return safeName.EndsWith("Conf", StringComparison.Ordinal) ? safeName : safeName + "Conf";
    }

    private string ToClassName(string value)
    {
        string name = ToFieldName(value);
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private string ToFieldName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                builder.Append(c);
            }
        }

        string name = builder.ToString();
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        if (char.IsDigit(name[0]))
        {
            name = "_" + name;
        }

        if (name.ToUpperInvariant() == name)
        {
            return name.ToLowerInvariant();
        }

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsDigit(value[0]))
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private bool IsSupportedType(string type)
    {
        if (IsEnum(type))
        {
            return true;
        }

        if (IsArray(type))
        {
            return IsSupportedType(type.Substring(0, type.Length - 2));
        }

        switch (type)
        {
            case "int":
            case "long":
            case "float":
            case "double":
            case "bool":
            case "string":
            case "Vector2":
            case "Vector3":
            case "Vector2Int":
            case "Vector3Int":
                return true;
            default:
                return false;
        }
    }

    private string ToCsType(string type)
    {
        if (IsEnum(type))
        {
            return type.Substring("enum_".Length);
        }

        return type;
    }

    private bool IsEnum(string type)
    {
        return type.StartsWith("enum_", StringComparison.Ordinal);
    }

    private bool IsArray(string type)
    {
        return type.EndsWith("[]", StringComparison.Ordinal);
    }

    private string[] SplitArray(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new string[0];
        }

        return value.Split(new[] { _settings.arraySeparator }, StringSplitOptions.None);
    }

    private string[] SplitVector(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new string[0];
        }

        return value.Split(new[] { _settings.vectorSeparator }, StringSplitOptions.None);
    }

    private int ParseInt(string value, string sourcePath, int row, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
        {
            return result;
        }

        throw new FormatException($"{sourcePath} 第 {row} 行字段 {fieldName} 不是 int: {value}");
    }

    private long ParseLong(string value, string sourcePath, int row, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0L;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
        {
            return result;
        }

        throw new FormatException($"{sourcePath} 第 {row} 行字段 {fieldName} 不是 long: {value}");
    }

    private float ParseFloat(string value, string sourcePath, int row, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0f;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        throw new FormatException($"{sourcePath} 第 {row} 行字段 {fieldName} 不是 float: {value}");
    }

    private double ParseDouble(string value, string sourcePath, int row, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
        {
            return result;
        }

        throw new FormatException($"{sourcePath} 第 {row} 行字段 {fieldName} 不是 double: {value}");
    }

    private bool ParseBool(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeKey(string value, string type, string sourcePath, int row, string fieldName)
    {
        if (IsEnum(type) || type == "int")
        {
            return ParseInt(value, sourcePath, row, fieldName).ToString(CultureInfo.InvariantCulture);
        }

        if (type == "long")
        {
            return ParseLong(value, sourcePath, row, fieldName).ToString(CultureInfo.InvariantCulture);
        }

        return value;
    }

    private static string NormalizeCell(object value)
    {
        return value == null ? string.Empty : value.ToString().Trim();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string EscapeSummary(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("*/", "* /");
    }

    private static string Indent(string text, string indent)
    {
        if (string.IsNullOrEmpty(indent) || string.IsNullOrEmpty(text))
        {
            return text;
        }

        return indent + text.Replace("\n", "\n" + indent);
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (Directory.Exists(assetFolder))
        {
            return;
        }

        Directory.CreateDirectory(assetFolder);
    }

    private static ConfigExportSettings LoadOrCreateSettings()
    {
        ConfigExportSettings settings = AssetDatabase.LoadAssetAtPath<ConfigExportSettings>(SETTINGS_PATH);
        if (settings != null)
        {
            return settings;
        }

        EnsureFolder(Path.GetDirectoryName(SETTINGS_PATH));
        settings = CreateInstance<ConfigExportSettings>();
        AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
        AssetDatabase.SaveAssets();
        return settings;
    }

    private sealed class SheetModel
    {
        public readonly string SourcePath;
        public readonly string SourceName;
        public readonly List<ConfigColumn> Columns = new List<ConfigColumn>();
        public readonly List<ConfigRow> Rows = new List<ConfigRow>();
        public ConfigColumn KeyColumn;

        public SheetModel(string sourcePath, string sourceName)
        {
            SourcePath = sourcePath;
            SourceName = sourceName;
        }
    }

    private readonly struct ConfigColumn
    {
        public readonly int SourceIndex;
        public readonly string Description;
        public readonly string TypeName;
        public readonly string FieldName;

        public ConfigColumn(int sourceIndex, string description, string typeName, string fieldName)
        {
            SourceIndex = sourceIndex;
            Description = description;
            TypeName = typeName;
            FieldName = fieldName;
        }
    }

    private sealed class ConfigRow
    {
        public readonly int ExcelRowIndex;
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

        public ConfigRow(int excelRowIndex)
        {
            ExcelRowIndex = excelRowIndex;
        }
    }
}
