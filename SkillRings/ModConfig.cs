namespace SkillRings
{
    internal class ModConfig
    {
        public int tier1SkillRingPrice { get; set; } = 1000;

        public int tier2SkillRingPrice { get; set; } = 5000;

        public int tier3SkillRingPrice { get; set; } = 10000;

        public int tier1SkillRingBoost { get; set; } = 1;

        public int tier2SkillRingBoost { get; set; } = 2;

        public int tier3SkillRingBoost { get; set; } = 5;

        public float tier1ExperienceRingBoost { get; set; } = 0.1f;

        public float tier2ExperienceRingBoost { get; set; } = 0.2f;

        public float tier3ExperienceRingBoost { get; set; } = 0.5f;

        public bool moddedSkillrings { get; set; } = true;
    }
}
