using UnityEngine;

namespace MatchRacers
{
    [CreateAssetMenu(fileName = "RaceCarCatalog", menuName = "MatchRacers/Race Car Catalog")]
    public sealed class RaceCarCatalogSO : ScriptableObject
    {
        [Header("Cars")]
        [SerializeField] private GameObject[] m_Cars = new GameObject[RaceConfigSO.CarCount];

        public GameObject GetPrefab(int carIndex)
        {
            if (m_Cars == null || carIndex < 0 || carIndex >= m_Cars.Length)
                return null;

            return m_Cars[carIndex];
        }
    }
}
