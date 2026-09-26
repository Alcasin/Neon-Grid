using System;
using NeonGrid.Presentation;
using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(menuName = "Neon Grid/City Building Art")]
    public sealed class CityBuildingArtDefinition : ScriptableObject
    {
        [SerializeField] private string chapterId;
        [SerializeField] private Sprite baseArchitecture;
        [SerializeField] private Sprite warmLights;
        [SerializeField] private Sprite electricalEnergy;
        [SerializeField] private Sprite restoredCore;
        [SerializeField] private Color baseTint = Color.white;
        [SerializeField] private Color warmTint = new Color(1f, 0.69f, 0.32f);
        [SerializeField] private Color energyTint = new Color(0.12f, 0.85f, 0.88f);
        [SerializeField] private Color coreTint = new Color(0.55f, 1f, 0.9f);
        [Tooltip("Base / warm / energy / core opacity, in M13 Locked through Restored order.")]
        [SerializeField] private Vector4[] stateOpacity =
        {
            new Vector4(0.65f, 0f, 0f, 0f),
            new Vector4(0.75f, 0.35f, 0f, 0f),
            new Vector4(0.85f, 0.65f, 0.3f, 0f),
            new Vector4(0.95f, 0.85f, 0.65f, 0f),
            new Vector4(1f, 1f, 1f, 0.85f)
        };
        [Tooltip("Fraction of the M13 visual rectangle. Includes transparent padding and label clearance.")]
        [SerializeField] private Vector2 footprintFraction = new Vector2(0.9f, 0.7f);
        [SerializeField] private Vector2 localOffset;
        [SerializeField, Range(0.1f, 1f)] private float localScale = 1f;

        public string ChapterId => chapterId;
        public Vector2 FootprintFraction => footprintFraction;
        public Vector2 LocalOffset => localOffset;
        public float LocalScale => localScale;

        public bool IsConfigured
        {
            get
            {
                if (string.IsNullOrWhiteSpace(chapterId) || baseArchitecture == null ||
                    stateOpacity == null || stateOpacity.Length != 5 ||
                    !Finite(localOffset.x) || !Finite(localOffset.y) ||
                    !UnitPositive(localScale) || !UnitPositive(footprintFraction.x) ||
                    !UnitPositive(footprintFraction.y)) return false;
                for (int layer = 0; layer < 4; layer++)
                {
                    Color tint = GetTint(layer);
                    for (int channel = 0; channel < 4; channel++)
                        if (!Finite(tint[channel]) || tint[channel] < 0f || tint[channel] > 1f)
                            return false;
                    for (int state = 0; state < 5; state++)
                    {
                        float opacity = stateOpacity[state][layer];
                        if (!Finite(opacity) || opacity < 0f || opacity > 1f ||
                            state > 0 && opacity < stateOpacity[state - 1][layer]) return false;
                    }
                }
                return baseTint.a > 0f && stateOpacity[0].x > 0f;
            }
        }

        public Sprite GetSprite(int layer)
        {
            switch (layer)
            {
                case 0: return baseArchitecture;
                case 1: return warmLights;
                case 2: return electricalEnergy;
                case 3: return restoredCore;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        public Color GetTint(int layer)
        {
            switch (layer)
            {
                case 0: return baseTint;
                case 1: return warmTint;
                case 2: return energyTint;
                case 3: return coreTint;
                default: throw new ArgumentOutOfRangeException(nameof(layer));
            }
        }

        public Vector4 GetOpacity(ChapterMapVisualState state) => stateOpacity[(int)state];
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool UnitPositive(float value) => Finite(value) && value > 0f && value <= 1f;

#if UNITY_EDITOR
        public void SetData(string association, Sprite architecture, Sprite warm, Sprite energy,
            Sprite core, Vector2 footprint)
        {
            chapterId = association;
            baseArchitecture = architecture;
            warmLights = warm;
            electricalEnergy = energy;
            restoredCore = core;
            footprintFraction = footprint;
        }
#endif
    }
}
