using System;
using System.IO;
using HollowElements.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace HollowElements.Settings
{
    public sealed class SettingsStore
    {
        private RuleConfig current = RuleConfig.Defaults();
        private bool migrated;
        private string migrationBackupSuffix;
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings {
            MissingMemberHandling = MissingMemberHandling.Error,
            Converters = { new StringEnumConverter { AllowIntegerValues = false } },
            Formatting = Formatting.Indented
        };
        public RuleConfig Snapshot() { return JsonConvert.DeserializeObject<RuleConfig>(Serialize(current), Json); }
        public static string Serialize(RuleConfig config) { return JsonConvert.SerializeObject(config, Json); }

        public bool TryApply(string text, out string message)
        {
            try
            {
                if (text == null || text.Length > 1024 * 1024) throw new ArgumentException("配置为空或超过 1MB。");
                var document=JObject.Parse(text);migrated=document.Value<int?>("SchemaVersion")==1;
                migrationBackupSuffix=migrated?".v1.bak":".pre-v017.bak";
                if(migrated)
                {
                    document["SchemaVersion"]=2;document["DirectDamageMultiplier"]=.45;
                    var reaction=document["Reactions"] as JObject ?? throw new ArgumentException("缺少反应配置。");
                    reaction.Remove("SuperconductBonus");reaction["ForwardMultiplier"]=4;reaction["ReverseMultiplier"]=2.5;
                    reaction["EnemyElectroNailScale"]=.45;reaction["SuperconductSeconds"]=3;
                    var attacks=document["Attacks"] as JObject ?? throw new ArgumentException("缺少招式配置。");
                    foreach(string source in new[]{"BlackShriek","BlackDiveBody","BlackDiveShock"}) if(!(attacks[source] is JObject)) throw new ArgumentException("缺少攻击来源："+source);
                    attacks["NativeAuxiliary"]=JObject.FromObject(RuleConfig.Defaults().Attacks[AttackSource.NativeAuxiliary],JsonSerializer.Create(Json));
                    attacks["BlackShriek"]["MaxApplicationsPerCast"]=2;attacks["BlackShriek"]["AttachmentHitStride"]=2;attacks["BlackShriek"]["SuperconductHitsPerCast"]=2;
                    foreach(string source in new[]{"BlackDiveBody","BlackDiveShock"}) {attacks[source]["AttachmentGroup"]=source;attacks[source]["SuperconductHitsPerCast"]=2;}
                }
                if(document.Value<int?>("BossRevision")==null)
                {
                    var reaction=document["Reactions"] as JObject ?? throw new ArgumentException("缺少反应配置。");
                    if(reaction.Value<double?>("WatcherSuperconductSeconds")==4) reaction["WatcherSuperconductSeconds"]=8;
                    document["BossRevision"]=1;migrated=true;
                }
                if(document.Value<int?>("AttackRevision")==null)
                {
                    var cyclone=document["Attacks"]?["Cyclone"] as JObject ?? throw new ArgumentException("缺少旋风斩配置。");
                    cyclone["AuraUnits"]=.5;cyclone["IcdSeconds"]=0;cyclone["HitStride"]=1;
                    cyclone["AttachmentHitStride"]=1;cyclone["MaxApplicationsPerCast"]=int.MaxValue;
                    document["AttackRevision"]=1;migrated=true;
                    if(migrationBackupSuffix!=".v1.bak") migrationBackupSuffix=".pre-v018.bak";
                }
                if((document.Value<int?>("AttackRevision")??0)<2)
                {
                    document["Attacks"]["Cyclone"]["AttachmentHitStride"]=2;
                    document["AttackRevision"]=2;migrated=true;
                    if(migrationBackupSuffix!=".v1.bak") migrationBackupSuffix=".pre-v0194.bak";
                }
                if(document.Value<int?>("CharmRevision")==null)
                {
                    var reaction=document["Reactions"] as JObject ?? throw new ArgumentException("缺少反应配置。");
                    if(reaction.Value<double?>("FrostSpeed")==.9) reaction["FrostSpeed"]=.8;
                    document["CharmRevision"]=1;migrated=true;
                    if(migrationBackupSuffix!=".v1.bak") migrationBackupSuffix=".pre-v019.bak";
                }
                if((document.Value<int?>("CharmRevision")??0)<2)
                {
                    var reaction=(JObject)document["Reactions"];
                    if(reaction.Value<double?>("SuperconductSeconds")==3) reaction["SuperconductSeconds"]=6;
                    if(reaction.Value<int?>("SuperconductLayers")==3) reaction["SuperconductLayers"]=4;
                    document["CharmRevision"]=2;migrated=true;
                    if(migrationBackupSuffix!=".v1.bak") migrationBackupSuffix=".pre-v0191.bak";
                }
                var candidate = document.ToObject<RuleConfig>(JsonSerializer.Create(Json));
                if (candidate == null) throw new ArgumentException("配置不能为空。");
                candidate.Validate();
                current = candidate;
                message = (migrated?"旧版参数已迁移至战斗平衡版本；":"参数已应用；")+"已有持续状态保留创建时的参数。";
                return true;
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is OverflowException)
            {
                message = "配置无效，保留上次有效参数：" + e.Message;
                return false;
            }
        }

        public bool TryReload(string path, out string message)
        {
            try
            {
                if(!TryApply(File.ReadAllText(path),out message)) return false;
                if(migrated)
                {
                    try {if(!File.Exists(path+migrationBackupSuffix)) File.Copy(path,path+migrationBackupSuffix);Save(path);}
                    catch(Exception e) when(e is IOException || e is UnauthorizedAccessException) {message+=" 迁移文件保存失败："+e.Message;}
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { message = "读取失败，保留上次有效参数：" + e.Message; return false; }
        }

        public void Save(string path)
        {
            string temp = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(temp, Serialize(current));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
    }

    public sealed class GlobalSettings
    {
        public bool BossMechanicHints=true;
        public bool DebugEnabled;
        public string DebugKey = "F8";
        public bool AllowAwayFromBench = true;
        public string ElementGuideKey = "F5";
        // Read older settings once; new saves write only the renamed key.
        [JsonProperty("CodexKey")] private string LegacyGuideKey { set { if(!string.IsNullOrEmpty(value)) ElementGuideKey=value; } }
        public float PopupScale = 1.5f;
        public bool PopupScaleMigrated;
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public float? DamagePopupScale;
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public float? ReactionPopupScale;
        public float PlayerFxScale = .65f;
        public float EnemyFxScale = 1;
        public float ReactionFxScale = 1.8f;
        public bool ShowHealthNumbers = true;
        public bool ShowHealthBars = true;
        public float SoundVolume = .55f;
        public bool TintAttacks = true;
        public float AttackTintStrength = .65f;
    }

    public sealed class Loadout
    {
        public CharmEquipment Charms = new CharmEquipment();
        public Element Nail = Element.Hydro;
        public Element Wave = Element.Pyro;
        public Element Dive = Element.Cryo;
        public Element Shriek = Element.Electro;
        public Element GreatSlash;
        public Element DashSlash;
        public Element Cyclone;
        public Element Get(AttackSlot slot)
        {
            switch (slot)
            {
                case AttackSlot.Nail: return Nail;
                case AttackSlot.Wave: return Wave;
                case AttackSlot.Dive: return Dive;
                case AttackSlot.Shriek: return Shriek;
                case AttackSlot.GreatSlash: return GreatSlash;
                case AttackSlot.DashSlash: return DashSlash;
                default: return Cyclone;
            }
        }
        public void Set(AttackSlot slot, Element element)
        {
            switch (slot)
            {
                case AttackSlot.Nail: Nail = element; break;
                case AttackSlot.Wave: Wave = element; break;
                case AttackSlot.Dive: Dive = element; break;
                case AttackSlot.Shriek: Shriek = element; break;
                case AttackSlot.GreatSlash: GreatSlash = element; break;
                case AttackSlot.DashSlash: DashSlash = element; break;
                case AttackSlot.Cyclone: Cyclone = element; break;
            }
        }
    }
}
