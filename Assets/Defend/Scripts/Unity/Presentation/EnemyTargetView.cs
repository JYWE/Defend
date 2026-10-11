using UnityEngine;

namespace Defend.Unity
{
    /// <summary>预存几何标记，只显示敌人已知的目标位置，不查询建筑位置。</summary>
    public sealed class EnemyTargetView : MonoBehaviour
    {
        [SerializeField] private Renderer[] lockedRenderers;
        [SerializeField] private Renderer[] searchRenderers;
        [SerializeField] private GameObject lockedShape;
        [SerializeField] private GameObject searchShape;
        private MaterialPropertyBlock properties;

        public void Configure(GameObject locked, Renderer[] lockedParts, GameObject search, Renderer[] searchParts)
        { lockedShape = locked; lockedRenderers = lockedParts; searchShape = search; searchRenderers = searchParts; }

        public void Apply(double knownX, double knownZ, bool searching, bool attacking)
        {
            transform.position = new Vector3((float)knownX, 0.13f, (float)knownZ);
            lockedShape.SetActive(!searching);
            searchShape.SetActive(searching);
            if (properties == null) properties = new MaterialPropertyBlock();
            Color color = searching ? new Color(1f, 0.68f, 0.12f) : attacking ? new Color(1f, 0.18f, 0.12f) : new Color(0.95f, 0.3f, 1f);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            foreach (Renderer part in searching ? searchRenderers : lockedRenderers) part.SetPropertyBlock(properties);
        }
    }
}
