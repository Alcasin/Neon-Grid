using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonGrid.Data
{
    [Serializable]
    public sealed class CityEnvironmentDistrictLayer
    {
        [SerializeField] private string chapterId;
        [SerializeField] private Sprite overlay;

        public string ChapterId => chapterId;
        public Sprite Overlay => overlay;

        public CityEnvironmentDistrictLayer(string chapterId, Sprite overlay)
        {
            this.chapterId = chapterId;
            this.overlay = overlay;
        }
    }

    [CreateAssetMenu(fileName = "CityEnvironmentArt",
        menuName = "Neon Grid/City Environment Art")]
    public sealed class CityEnvironmentArtDefinition : ScriptableObject
    {
        public const string PowerStationId = "power_station";
        public const string SubstationId = "substation";
        public const string ControlCenterId = "control_center";
        public const string AutomationPlantId = "automation_plant";
        public const string CentralGridId = "central_grid";

        private static readonly string[] RequiredDistrictIds =
        {
            PowerStationId, SubstationId, ControlCenterId, AutomationPlantId, CentralGridId
        };

        [SerializeField] private Sprite baseCity;
        [SerializeField] private List<CityEnvironmentDistrictLayer> districts =
            new List<CityEnvironmentDistrictLayer>();
        [SerializeField] private Sprite finalAccent;
        [SerializeField] private Vector2 footprintFraction = Vector2.one;
        [SerializeField] private Vector2 localOffset;

        public Sprite BaseCity => baseCity;
        public IReadOnlyList<CityEnvironmentDistrictLayer> Districts => districts;
        public Sprite FinalAccent => finalAccent;
        public Vector2 FootprintFraction => footprintFraction;
        public Vector2 LocalOffset => localOffset;

        public bool IsConfigured
        {
            get
            {
                if (baseCity == null || districts == null ||
                    districts.Count != RequiredDistrictIds.Length ||
                    footprintFraction.x <= 0f || footprintFraction.x > 1f ||
                    footprintFraction.y <= 0f || footprintFraction.y > 1f ||
                    !Finite(localOffset.x) || !Finite(localOffset.y)) return false;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (CityEnvironmentDistrictLayer district in districts)
                    if (district == null || district.Overlay == null ||
                        string.IsNullOrWhiteSpace(district.ChapterId) ||
                        !ids.Add(district.ChapterId) ||
                        !RegisteredWithBase(district.Overlay)) return false;
                foreach (string required in RequiredDistrictIds)
                    if (!ids.Contains(required)) return false;
                return finalAccent == null || RegisteredWithBase(finalAccent);
            }
        }

        public Sprite GetDistrictOverlay(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId) || districts == null) return null;
            foreach (CityEnvironmentDistrictLayer district in districts)
                if (district != null && string.Equals(district.ChapterId, chapterId,
                        StringComparison.Ordinal)) return district.Overlay;
            return null;
        }

        private bool RegisteredWithBase(Sprite sprite) => sprite.rect.size == baseCity.rect.size &&
                                                           sprite.pivot == baseCity.pivot;

        private static bool Finite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

#if UNITY_EDITOR
        public void SetData(Sprite cityBase, IEnumerable<CityEnvironmentDistrictLayer> layers,
            Sprite allCityAccent, Vector2 footprint, Vector2 offset)
        {
            baseCity = cityBase;
            districts = layers == null
                ? new List<CityEnvironmentDistrictLayer>()
                : new List<CityEnvironmentDistrictLayer>(layers);
            finalAccent = allCityAccent;
            footprintFraction = footprint;
            localOffset = offset;
        }
#endif
    }
}
