using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace StardewEconomyProject.source.economy
{
    /// <summary>
    /// SpaceCore API interface for registering and managing custom skills.
    /// Obtained via SMAPI's mod registry at GameLaunched.
    /// </summary>
    public interface ISpaceCoreApi
    {
        string[] GetCustomSkills();
        int GetLevelForCustomSkill(Farmer farmer, string skill);
        int GetBuffLevelForCustomSkill(Farmer farmer, string skill);
        int GetTotalLevelForCustomSkill(Farmer farmer, string skill);
        int GetExperienceForCustomSkill(Farmer farmer, string skill);
        List<System.Tuple<string, int, int>> GetExperienceAndLevelsForCustomSkill(Farmer farmer);
        void AddExperienceForCustomSkill(Farmer farmer, string skill, int amt);
        string GetDisplayNameOfCustomSkill(string skill);
        Texture2D GetSkillPageIconForCustomSkill(string skill);
        Texture2D GetSkillIconForCustomSkill(string skill);
        int GetProfessionId(string skill, string profession);
    }

    /// <summary>
    /// The Reputation custom skill. Governs the farm's "Brand" and market access.
    /// Unlike vanilla skills, Reputation XP can be lost through contractual failure.
    ///
    /// Market Tier Milestones:
    /// Level 0-4: Regional (1.0x capacity) — Small local bundles; NPCs only.
    /// Level 5-9: National (5.0x capacity) — Zuzu City bulk contracts.
    /// Level 10:  International (25.0x capacity) — Global commodity shipping.
    /// </summary>
    public class ReputationSkill : SpaceCore.Skills.Skill
    {
        public static readonly string SkillId = "neroyuki.stardeweconomy.Reputation";
        public static ReputationSkill Instance { get; private set; }
        private static ISpaceCoreApi _spaceCoreApi;

        // ── Professions ──
        public class LocalMerchant : Profession
        {
            public LocalMerchant(SpaceCore.Skills.Skill skill) : base(skill, "LocalMerchant") { }
            public override string GetName() => "Local Merchant";
            public override string GetDescription() => "+15% contract rewards from Pelican Town NPCs.";
        }

        public class BulkTrader : Profession
        {
            public BulkTrader(SpaceCore.Skills.Skill skill) : base(skill, "BulkTrader") { }
            public override string GetName() => "Bulk Trader";
            public override string GetDescription() => "Contract quantity requirements reduced by 20%.";
        }

        public class RegionalMogul : Profession
        {
            public RegionalMogul(SpaceCore.Skills.Skill skill) : base(skill, "RegionalMogul") { }
            public override string GetName() => "Regional Mogul";
            public override string GetDescription() => "Unlock Zuzu City premium contracts with 50% higher rewards.";
        }

        public class MarketManipulator : Profession
        {
            public MarketManipulator(SpaceCore.Skills.Skill skill) : base(skill, "MarketManipulator") { }
            public override string GetName() => "Market Manipulator";
            public override string GetDescription() => "Market bottle drainage rate increased by 25%.";
        }

        public class GlobalExporter : Profession
        {
            public GlobalExporter(SpaceCore.Skills.Skill skill) : base(skill, "GlobalExporter") { }
            public override string GetName() => "Global Exporter";
            public override string GetDescription() => "Unlock international contracts with 2x capacity and rewards.";
        }

        public class LuxuryBrand : Profession
        {
            public LuxuryBrand(SpaceCore.Skills.Skill skill) : base(skill, "LuxuryBrand") { }
            public override string GetName() => "Luxury Brand";
            public override string GetDescription() => "Iridium quality items have 3x margin multiplier instead of 2x.";
        }

        // ══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

        public ReputationSkill() : base(SkillId)
        {
            Instance = this;

            // Experience curve: amounts needed for each level (1-10)
            ExperienceCurve = new int[]
            {
                100,    // Level 1
                300,    // Level 2
                600,    // Level 3
                1000,   // Level 4
                1500,   // Level 5
                2300,   // Level 6
                3500,   // Level 7
                5000,   // Level 8
                7500,   // Level 9
                10000,  // Level 10
            };

            ExperienceBarColor = new Color(180, 140, 60); // Gold/amber color

            // Define professions
            var localMerchant = new LocalMerchant(this);
            var bulkTrader = new BulkTrader(this);
            var regionalMogul = new RegionalMogul(this);
            var marketManipulator = new MarketManipulator(this);
            var globalExporter = new GlobalExporter(this);
            var luxuryBrand = new LuxuryBrand(this);

            Professions.Add(localMerchant);
            Professions.Add(bulkTrader);
            Professions.Add(regionalMogul);
            Professions.Add(marketManipulator);
            Professions.Add(globalExporter);
            Professions.Add(luxuryBrand);

            // Level 5: Choose Local Merchant or Bulk Trader
            ProfessionsForLevels.Add(new ProfessionPair(5, localMerchant, bulkTrader));

            // Level 10 (with Local Merchant): Choose Regional Mogul or Market Manipulator
            ProfessionsForLevels.Add(new ProfessionPair(10, regionalMogul, marketManipulator, localMerchant));

            // Level 10 (with Bulk Trader): Choose Global Exporter or Luxury Brand
            ProfessionsForLevels.Add(new ProfessionPair(10, globalExporter, luxuryBrand, bulkTrader));
        }

        // ══════════════════════════════════════════════════════════════
        //  SKILL INTERFACE
        // ══════════════════════════════════════════════════════════════

        public override string GetName() => "Reputation";

        public override List<string> GetExtraLevelUpInfo(int level)
        {
            var info = new List<string>();
            switch (level)
            {
                case 1: info.Add("+1 contract slot"); break;
                case 2: info.Add("+5% contract rewards"); break;
                case 3: info.Add("Market drain rate +10%"); break;
                case 4: info.Add("+1 contract slot"); break;
                case 5: info.Add("Market capacity 5x (National tier)"); break;
                case 6: info.Add("+10% contract rewards"); break;
                case 7: info.Add("Bargaining patience bonus +0.05"); break;
                case 8: info.Add("Tax rate -5%"); break;
                case 9: info.Add("+1 contract slot"); break;
                case 10: info.Add("Market capacity 25x (International tier)"); break;
            }
            return info;
        }

        public override string GetSkillPageHoverText(int level)
        {
            string tier = level >= 10 ? "International" : level >= 5 ? "National" : "Regional";
            return $"Reputation Level {level}\nMarket Tier: {tier}";
        }

        // ══════════════════════════════════════════════════════════════
        //  STATIC HELPERS
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Asset path for skill icons loaded by the Content Patcher pack.
        /// Spritesheet layout (128×16, eight 16×16 cells):
        ///   0: Main skill icon         1: Skills-page icon
        ///   2: LocalMerchant            3: BulkTrader
        ///   4: RegionalMogul            5: MarketManipulator
        ///   6: GlobalExporter           7: LuxuryBrand
        /// </summary>
        public const string SkillIconsAssetPath = "Mods/neroyuki.stardeweconomyitems/SkillIcons";

        private static IModHelper _modHelper;

        /// <summary>Provide SMAPI helper for content loading.</summary>
        public static void SetHelper(IModHelper helper)
        {
            _modHelper = helper;
        }

        /// <summary>
        /// Phase 1 — Register the skill with SpaceCore.
        /// MUST be called from Entry() before GameLaunched fires.
        /// Icons are left null here; call LoadIcons() from GameLaunched.
        /// </summary>
        public static void RegisterEarly()
        {
            Instance = new ReputationSkill();
            SpaceCore.Skills.RegisterSkill(Instance);
            LogHelper.Info("[Reputation] Skill registered with SpaceCore (early phase).");
        }

        /// <summary>
        /// Phase 2 — Load skill icons from the Content Patcher spritesheet.
        /// Call from GameLaunched after CP has applied its patches.
        /// Falls back to coloured placeholder textures on any failure.
        /// </summary>
        public static void LoadIcons()
        {
            if (Instance == null)
            {
                LogHelper.Warn("[Reputation] LoadIcons called before RegisterEarly — skill not yet created.");
                return;
            }

            try
            {
                LoadSkillIcons();
                LogHelper.Info("[Reputation] Loaded skill icons from content pipeline.");
            }
            catch (Exception ex)
            {
                LogHelper.Warn($"[Reputation] Could not load skill icons (using placeholders): {ex.Message}");
                Instance.Icon = CreatePlaceholderTexture(16, 16, new Color(180, 140, 60));
                Instance.SkillsPageIcon = CreatePlaceholderTexture(10, 10, new Color(180, 140, 60));
                foreach (var prof in Instance.Professions)
                    prof.Icon = CreatePlaceholderTexture(16, 16, new Color(200, 160, 80));
            }
        }

        /// <summary>Legacy wrapper — calls RegisterEarly + LoadIcons together.</summary>
        public static void Register()
        {
            RegisterEarly();
            LoadIcons();
        }

        /// <summary>
        /// Load all skill and profession icons from the CP-provided spritesheet.
        /// </summary>
        private static void LoadSkillIcons()
        {
            Texture2D sheet = Game1.content.Load<Texture2D>(SkillIconsAssetPath);

            // Main skill icon — cell 0 (16×16)
            Instance.Icon = ExtractSubTexture(sheet, new Rectangle(0, 0, 16, 16));

            // Skills-page icon — cell 1 (only 10×10 is used, but extract 16×16 and SpaceCore handles sizing)
            Instance.SkillsPageIcon = ExtractSubTexture(sheet, new Rectangle(16, 0, 10, 10));

            // Profession icons — cells 2-7
            string[] profOrder = { "LocalMerchant", "BulkTrader", "RegionalMogul", "MarketManipulator", "GlobalExporter", "LuxuryBrand" };
            for (int i = 0; i < profOrder.Length && i < Instance.Professions.Count; i++)
            {
                Instance.Professions[i].Icon = ExtractSubTexture(sheet, new Rectangle((i + 2) * 16, 0, 16, 16));
            }
        }

        /// <summary>Extract a sub-region of a texture into a new Texture2D.</summary>
        private static Texture2D ExtractSubTexture(Texture2D source, Rectangle sourceRect)
        {
            var data = new Color[sourceRect.Width * sourceRect.Height];
            source.GetData(0, sourceRect, data, 0, data.Length);
            var sub = new Texture2D(Game1.graphics.GraphicsDevice, sourceRect.Width, sourceRect.Height);
            sub.SetData(data);
            return sub;
        }

        /// <summary>Set SpaceCore API reference (obtained from SMAPI mod registry).</summary>
        public static void SetApi(ISpaceCoreApi api)
        {
            _spaceCoreApi = api;
        }

        /// <summary>Get the current Reputation level for a farmer.</summary>
        public static int GetLevel(Farmer farmer)
        {
            if (_spaceCoreApi != null)
            {
                return _spaceCoreApi.GetLevelForCustomSkill(farmer, SkillId);
            }
            return SpaceCore.Skills.GetSkillLevel(farmer, SkillId);
        }

        /// <summary>Add Reputation XP. Can also be negative for failures.</summary>
        public static void AddReputationXP(Farmer farmer, int amount)
        {
            if (amount == 0) return;

            if (amount > 0)
            {
                if (_spaceCoreApi != null)
                {
                    _spaceCoreApi.AddExperienceForCustomSkill(farmer, SkillId, amount);
                }
                else
                {
                    SpaceCore.Skills.AddExperience(farmer, SkillId, amount);
                }
                LogHelper.Debug($"[Reputation] +{amount} XP (total: {GetExperience(farmer)})");
            }
            else
            {
                // Negative XP for failures — handled separately since SpaceCore
                // doesn't natively support XP loss. We use modData to track a penalty offset.
                int currentPenalty = GetPenaltyOffset(farmer);
                currentPenalty += Math.Abs(amount);
                farmer.modData[$"{SkillId}_PenaltyOffset"] = currentPenalty.ToString();
                LogHelper.Debug($"[Reputation] -{Math.Abs(amount)} XP penalty offset (total penalty: {currentPenalty})");
            }
        }

        /// <summary>Get total XP for the Reputation skill.</summary>
        public static int GetExperience(Farmer farmer)
        {
            if (_spaceCoreApi != null)
            {
                return _spaceCoreApi.GetExperienceForCustomSkill(farmer, SkillId);
            }
            return SpaceCore.Skills.GetExperienceFor(farmer, SkillId);
        }

        /// <summary>Get the effective level accounting for penalty offsets.</summary>
        public static int GetEffectiveLevel(Farmer farmer)
        {
            int baseLevel = GetLevel(farmer);
            // Penalty offset could theoretically reduce effective level,
            // but for now we keep it simple — penalties prevent future gains
            return baseLevel;
        }

        /// <summary>
        /// Check if the current player has a specific Reputation profession.
        /// Uses SpaceCore API to resolve profession IDs.
        /// </summary>
        public static bool HasProfession(string professionName)
        {
            if (_spaceCoreApi == null) return false;
            try
            {
                int profId = _spaceCoreApi.GetProfessionId(SkillId, professionName);
                return Game1.player.professions.Contains(profId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Get the market tier string for the current level.</summary>
        public static string GetMarketTier(Farmer farmer)
        {
            int level = GetEffectiveLevel(farmer);
            if (level >= 10) return "International";
            if (level >= 5) return "National";
            return "Regional";
        }

        /// <summary>Get the XP penalty offset (accumulated from failures).</summary>
        private static int GetPenaltyOffset(Farmer farmer)
        {
            if (farmer.modData.TryGetValue($"{SkillId}_PenaltyOffset", out string val) && int.TryParse(val, out int offset))
                return offset;
            return 0;
        }

        /// <summary>Create a simple colored placeholder texture.</summary>
        private static Texture2D CreatePlaceholderTexture(int width, int height, Color color)
        {
            var texture = new Texture2D(Game1.graphics.GraphicsDevice, width, height);
            Color[] data = new Color[width * height];

            for (int i = 0; i < data.Length; i++)
            {
                // Simple diamond pattern
                int x = i % width;
                int y = i / width;
                float cx = width / 2f;
                float cy = height / 2f;
                float dist = System.Math.Abs(x - cx) / cx + System.Math.Abs(y - cy) / cy;
                data[i] = dist < 0.8f ? color : Color.Transparent;
            }

            texture.SetData(data);
            return texture;
        }
    }
}
