using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using ExcelDataReader;
using System.Data;

public class ConfigExporterEditor : EditorWindow
{
    private string excelFolder = "Assets/ExcelConfigs"; 
    private string codeFolder = "Assets/Scripts/Config/Generated"; 
    private string extFolder = "Assets/Scripts/Config/Extensions"; 
    private string bytesFolder = "Assets/AddressableResources/ConfigData"; 

    private const byte XOR_KEY = 0x55;

    [MenuItem("Tools/Framework/配置表一键导出")]
    public static void ShowWindow()
    {
        GetWindow<ConfigExporterEditor>("配置导出工具");
    }

    private void OnGUI()
    {
        GUILayout.Label("配置表转换设置", EditorStyles.boldLabel);
        excelFolder = EditorGUILayout.TextField("Excel目录", excelFolder);
        bytesFolder = EditorGUILayout.TextField("二进制输出", bytesFolder);
        
        if (GUILayout.Button("开始一键导出 (代码 + 数据)", GUILayout.Height(40)))
        {
            ProcessAllConfigs();
        }
    }

    private void ProcessAllConfigs()
    {
        if (!Directory.Exists(excelFolder)) { Debug.LogError("找不到Excel目录"); return; }
        
        Directory.CreateDirectory(codeFolder);
        Directory.CreateDirectory(extFolder);
        Directory.CreateDirectory(bytesFolder);

        // 【修改 1】：将 SearchOption 改为 AllDirectories，确保能搜到子目录里的表
        string[] files = Directory.GetFiles(excelFolder, "*.xlsx", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            if (file.Contains("~$")) continue; 

            // 【修改 2】：统一路径分隔符，判断是否在 Ignores 子目录下
            string normalizedPath = file.Replace('\\', '/');
            bool isIgnored = normalizedPath.Contains("/Ignores/");

            // 传入判定标记
            ExportSingleFile(file, !isIgnored);
        }

        AssetDatabase.Refresh(); 
        Debug.Log("<color=green>[Config] 所有配置导出成功！</color>");
    }

    // 【修改 3】：接收 generateCode 标记
    private void ExportSingleFile(string path, bool generateCode)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        string className = fileName + "Config";

        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables[0]; 

                List<int> keepIndices = new List<int>();
                for (int col = 0; col < table.Columns.Count; col++)
                {
                    if (table.Rows[1][col].ToString().Trim().ToUpper() == "A")
                        keepIndices.Add(col);
                }

                // 【核心逻辑】：如果在 Ignores 目录下，跳过代码生成！
                if (generateCode)
                {
                    GenerateCode(className, table, keepIndices);
                    GenerateExtensionCode(className);
                    Debug.Log($"生成代码与数据: {fileName}");
                }
                else
                {
                    Debug.Log($"<color=#00FFFF>跳过代码生成，仅导出数据: {fileName}</color>");
                }

                // 二进制数据是无论如何都要导出的
                ExportBinary(fileName, table, keepIndices);
            }
        }
    }

    private void GenerateCode(string className, DataTable table, List<int> keepIndices)
    {
        StringBuilder fields = new StringBuilder();
        StringBuilder readLogic = new StringBuilder();

        foreach (int col in keepIndices)
        {
            string desc = table.Rows[0][col].ToString();
            string type = table.Rows[2][col].ToString();
            string name = table.Rows[3][col].ToString();
            string fieldName = char.ToLower(name[0]) + name.Substring(1);

            string csType = type.StartsWith("enum_") ? type.Replace("enum_", "") : type;
            fields.AppendLine($"    /// <summary> {desc} </summary>");
            fields.AppendLine($"    public {csType} {fieldName};");

            switch (type)
            {
                case "int": readLogic.AppendLine($"                    item.{fieldName} = br.ReadInt32();"); break;
                case "long": readLogic.AppendLine($"                    item.{fieldName} = br.ReadInt64();"); break;
                case "float": readLogic.AppendLine($"                    item.{fieldName} = br.ReadSingle();"); break;
                case "double": readLogic.AppendLine($"                    item.{fieldName} = br.ReadDouble();"); break;
                case "string": 
                    readLogic.AppendLine($"                    int len_{col} = br.ReadInt32();");
                    readLogic.AppendLine($"                    item.{fieldName} = System.Text.Encoding.UTF8.GetString(br.ReadBytes(len_{col}));"); 
                    break;
                case "Vector2": readLogic.AppendLine($"                    item.{fieldName} = new Vector2(br.ReadSingle(), br.ReadSingle());"); break;
                case "Vector3": readLogic.AppendLine($"                    item.{fieldName} = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());"); break;
                default:
                    if (type.StartsWith("enum_"))
                        readLogic.AppendLine($"                    item.{fieldName} = ({csType})br.ReadInt32();");
                    break;
            }
        }

        string template = $@"// ------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具自动生成，请勿手动修改。
// </auto-generated>
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class {className} : ConfigManagerBase<{className}>
{{
{fields}
    public static void Load(byte[] data)
    {{
        Clear();
        byte xorKey = {XOR_KEY};
        for (int i = 0; i < data.Length; i++) {{ data[i] ^= xorKey; }}

        using (MemoryStream ms = new MemoryStream(data))
        using (BinaryReader br = new BinaryReader(ms))
        {{
            int count = br.ReadInt32();
            for (int i = 0; i < count; i++)
            {{
                {className} item = new {className}();
{readLogic}
                List.Add(item);
                item.OnPostLoad();
            }}
        }}
        OnAllLoadDone();
    }}

    partial void OnPostLoad();
    static partial void OnAllLoadDone();
}}";
        File.WriteAllText($"{codeFolder}/{className}Generated.cs", template);
    }

    private void GenerateExtensionCode(string className)
    {
        string path = $"{extFolder}/{className}Ext.cs";
        if (File.Exists(path)) return; 

        string template = $@"using UnityEngine;

public partial class {className}
{{
    partial void OnPostLoad()
    {{
        // TODO: 加入字典
        // Dict[this.id] = this; 
    }}

    static partial void OnAllLoadDone()
    {{
    }}
}}";
        File.WriteAllText(path, template);
    }

    private void ExportBinary(string fileName, DataTable table, List<int> keepIndices)
    {
        using (MemoryStream ms = new MemoryStream())
        using (BinaryWriter bw = new BinaryWriter(ms))
        {
            int rowCount = table.Rows.Count - 4; 
            bw.Write(rowCount);

            for (int r = 4; r < table.Rows.Count; r++)
            {
                foreach (int c in keepIndices)
                {
                    string type = table.Rows[2][c].ToString();
                    string val = table.Rows[r][c].ToString();

                    if (type == "int" || type.StartsWith("enum_")) bw.Write(int.Parse(val));
                    else if (type == "long") bw.Write(long.Parse(val));
                    else if (type == "float") bw.Write(float.Parse(val));
                    else if (type == "double") bw.Write(double.Parse(val));
                    else if (type == "string")
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(val);
                        bw.Write(bytes.Length);
                        bw.Write(bytes);
                    }
                    else if (type == "Vector2" || type == "Vector3")
                    {
                        string[] parts = val.Split('|');
                        bw.Write(float.Parse(parts[0]));
                        bw.Write(float.Parse(parts[1]));
                        if (type == "Vector3") bw.Write(float.Parse(parts[2]));
                    }
                }
            }

            byte[] raw = ms.ToArray();
            for (int i = 0; i < raw.Length; i++) raw[i] ^= XOR_KEY;
            File.WriteAllBytes($"{bytesFolder}/{fileName}Conf.bytes", raw);
        }
    }
}