using UnityEngine;

public class ConfigExportSettings : ScriptableObject
{
    public string excelFolder = "Assets/ExcelConfigs";
    public string generatedCodeFolder = "Assets/SGFCore/Modules/Config/Preset/Generated";
    public string extensionCodeFolder = "Assets/SGFCore/Modules/Config/Preset/Extensions";
    public string bytesFolder = "Assets/AddressableResources/ConfigData";

    public string namespaceName = string.Empty;
    public string keyFieldName = "id";
    public string languageClassName = "LanguageConf";
    public string languageSourcePrefix = "LanguageConf_";
    public string languageBytesPrefix = "LanguageTableConf";
    public string defaultLanguageSuffix = "Default";

    public bool configureAddressables = false;
    public string addressablesGroupName = "ConfigData";

    public string clientFlag = "A";
    public string arraySeparator = "|";
    public string vectorSeparator = "|";
}
