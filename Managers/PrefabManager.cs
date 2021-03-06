using UnityEngine;

public class PrefabManager : MonoBehaviour
{
    public static PrefabManager instance;

    [Header("Units Prefabs")]
    public GameObject villagerPlayerPrefab;
    public GameObject villagerEnemyPrefab;
    public string weaponInHandName;

    [Header("Buildings Prefabs")]
    public GameObject resourceCampPlayerPrefab;
    public GameObject resourceCampConstructionPlayerPrefab;
    //public GameObject resourceCampEnemyPrefab;
    //public GameObject resourceCampEnemyConstructionPrefab;
    public GameObject villagerInnPlayerPrefab;
    public GameObject villagerInnConstructionPlayerPrefab;

    [Header("ResourceFields Prefabs")]
    public GameObject berryBushSmallPrefab;
    public GameObject berryBushLargePrefab;
    public GameObject lumberTreePrefab;
    public GameObject goldOreMinePrefab;
    public GameObject farmFieldSmallPrefab;
    public GameObject farmFieldLargePrefab;

    [Header("Resource Drops Prefabs")]
    public GameObject berriesDropPrefab;
    public GameObject logPileDropPrefab;
    public GameObject goldOreDropPrefab;
    public GameObject farmDropPrefab;

    /*[Header("Units Stats")]
    public UnitStats playerVillagerStats;
    public UnitStats enemyVillagerStats;

    [Header("Buildings Stats")]
    public BuildingStats playerResourceCamp;*/

    [Header("Resource Info")]
    public ResourceInfo berriesInfo;
    public ResourceInfo woodInfo;
    public ResourceInfo goldInfo;
    public ResourceInfo farmInfo;

    [Header("Transform Parents")]
    public GameObject unitsTransformParentGO;
    public GameObject buildingsTransformParentGO;
    public GameObject resourceFieldsTransformParentGO;
    public GameObject resourceDropsTransformParentGO;


    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Debug.LogError("Another prefab manager present.");
    }
}
