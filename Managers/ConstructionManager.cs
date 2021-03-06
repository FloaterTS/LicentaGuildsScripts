using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ConstructionManager : MonoBehaviour
{
    public static ConstructionManager instance;

    [SerializeField] private GameObject previewResourceCamp;
    [SerializeField] private GameObject previewVillagerInn;

    private GameObject previewBuildingGO;
    private GameObject underConstructionBuildingPrefab;
    private GameObject constructedBuildingPrefab;
    private PlacementValidity previewPlacementValidity;
    private readonly float snapRotationDegrees = 45f;
    private bool isPreviewingBuildingConstruction = false;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Debug.LogError("Another unit selection manager present.");
    }

    void Update()
    {
        if (isPreviewingBuildingConstruction)
        {
            if (GameManager.instance.IsPaused())
            {
                StopPreviewBuildingGO();
                StopPreviewBuildingBool();
            }

            if (previewBuildingGO != null)
            {
                RotatePreviewBuilding();
                Ray previewRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(previewRay, out RaycastHit previewHit, 1000f, LayerMask.GetMask("Terrain")))
                    previewBuildingGO.transform.position = previewHit.point;
            }

            if (UIManager.instance.IsMouseOverUI())
                return;

            if (Input.GetMouseButtonDown(0))
                if(previewPlacementValidity.IsValidlyPlaced())
                    StartConstructionForSelection();

            if (Input.GetMouseButtonDown(1))
                StopPreviewBuildingGO();

            if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1))
                if (previewBuildingGO == null)
                    StopPreviewBuildingBool();
        }
    }

    public void StartPreviewResourceCampConstruction()
    {
        StartPreviewConstruction(PrefabManager.instance.resourceCampConstructionPlayerPrefab, PrefabManager.instance.resourceCampPlayerPrefab, previewResourceCamp);
    }

    public void StartPreviewVillagerInnConstruction()
    {
        StartPreviewConstruction(PrefabManager.instance.villagerInnConstructionPlayerPrefab, PrefabManager.instance.villagerInnPlayerPrefab, previewVillagerInn);
    }

    private void StartPreviewConstruction(GameObject constructionPrefab, GameObject buildingPrefab, GameObject previewPrefab)
    {
        if (previewBuildingGO != null)
            Destroy(previewBuildingGO);

        Building building = constructionPrefab.GetComponent<Building>();
        if(building == null)
        {
            Debug.LogError("Construction building prefab " + constructionPrefab + " doesn't have Building script attached");
            return;
        }

        if (!ResourceManager.instance.UseResources(building.buildingStats.buildingCost, true))
            return;

        previewBuildingGO = Instantiate(previewPrefab);
        previewBuildingGO.transform.eulerAngles = new Vector3(0f, FaceCameraInitialPreviewRotation(), 0f);
        previewPlacementValidity = previewBuildingGO.GetComponent<PlacementValidity>();

        underConstructionBuildingPrefab = constructionPrefab;
        constructedBuildingPrefab = buildingPrefab;
        isPreviewingBuildingConstruction = true;
    }

    public void StopPreviewBuildingGO()
    {
        Destroy(previewBuildingGO);
    }

    public void StopPreviewBuildingBool()
    {
        isPreviewingBuildingConstruction = false;
    }

    public bool IsPreviewingBuilding()
    {
        return isPreviewingBuildingConstruction;
    }

    private void RotatePreviewBuilding()
    {
        if(Input.GetKeyDown(KeyCode.Q))
            previewBuildingGO.transform.eulerAngles -= new Vector3(0f, snapRotationDegrees, 0f);
        if (Input.GetKeyDown(KeyCode.E))
            previewBuildingGO.transform.eulerAngles += new Vector3(0f, snapRotationDegrees, 0f);
    }

    private float FaceCameraInitialPreviewRotation()
    {
        float closestSnapValue = 0f;
        float minDifference = 360f;
        float degrees = -180f;
        float difference;

        while(degrees <= 180f)
        {
            difference = Mathf.Abs(Camera.main.transform.eulerAngles.y - degrees);
            if (difference < minDifference)
            {
                minDifference = difference;
                closestSnapValue = degrees;
            }
            degrees += snapRotationDegrees;
        }
        return closestSnapValue + 180f;
    }

    private void StartConstructionForSelection()
    {
        if (!ResourceManager.instance.UseResources(underConstructionBuildingPrefab.GetComponent<Building>().buildingStats.buildingCost, false))
            return;

        GameObject inConstructionBuildingGO = Instantiate(underConstructionBuildingPrefab, previewBuildingGO.transform.position, previewBuildingGO.transform.rotation, PrefabManager.instance.buildingsTransformParentGO.transform);
        UnderConstruction underConstruction = inConstructionBuildingGO.GetComponent<UnderConstruction>();
        underConstruction.constructedBuildingPrefab = constructedBuildingPrefab;
        Building building = inConstructionBuildingGO.GetComponent<Building>();
        building.SetCurrentHitpoints(0.1f);

        StopPreviewBuildingGO();

        foreach (Unit unit in SelectionManager.instance.selectedUnits)
            if(unit.worker != null)
                unit.worker.StartConstruction(inConstructionBuildingGO);
    }

}
