﻿using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Unit))]
public class Worker : MonoBehaviour
{
    public CarriedResource carriedResource;

    private Unit unit;
    private Animator animator;
    private bool onWayToTask = false;
    private bool constructionSiteReached = false;
 

    void Awake()
    {
        unit = GetComponent<Unit>();
        animator = GetComponent<Animator>();

        carriedResource.amount = 0;
    }

    public bool IsBusy()
    {
        return onWayToTask;
    }

    public void SetConstructionSiteReached(bool reached)
    {
        constructionSiteReached = reached;
    }

    public void CollectResource(ResourceField targetResource)
    {
        StartCoroutine(CollectResourceCo(targetResource));
    }

    public void StoreResource(ResourceCamp resourceCamp, bool backToResource = false)
    {
        StartCoroutine(StoreResourceCo(resourceCamp, backToResource));
    }

    public void StoreResourceInClosestCamp()
    {
        StartCoroutine(StoreResourceInClosestCampCo());
    }

    public void StartConstruction(GameObject underConstructionBuilding)
    {
        StartCoroutine(StartConstructionCo(underConstructionBuilding));
    }

    public void DropResourceAction()
    {
        StartCoroutine(LetDownDropResourceCo());
    }

    public void PickUpResourceAction(ResourceDrop resourceDrop)
    {
        StartCoroutine(PickUpResourceCo(resourceDrop));
    }

    public void LiftResorce(bool instant = false)
    {
        StartCoroutine(LiftResourceCo(instant));
    }

    public IEnumerator StopTaskCo()
    {
        unit.unitState = UnitState.IDLE;
        animator.SetBool("working", false);
        yield return StartCoroutine(unit.NavObstacleToNavAgent());
    }

    public IEnumerator StopWorkActionCo()
    {
        if (carriedResource.amount > 0)
        {
            if (unit.unitState == UnitState.WORKING)
                yield return StartCoroutine(LetDownDropResourceCo(false));
            else
                yield return StartCoroutine(LetDownDropResourceCo());
        }
        yield return StartCoroutine(StopTaskCo());
    }

    private IEnumerator StartConstructionCo(GameObject underConstructionBuilding)
    {
        if (unit.target == underConstructionBuilding.transform.position)
            yield break;

        yield return StartCoroutine(unit.CheckIfImmobileCo());

        yield return StartCoroutine(GoToConstructionSiteCo(underConstructionBuilding));

        if (underConstructionBuilding == null || unit.target != underConstructionBuilding.transform.position)
            yield break;

        yield return StartCoroutine(ConstructBuildingCo(underConstructionBuilding));
    }

    private IEnumerator GoToConstructionSiteCo(GameObject underConstructionBuilding)
    {
        yield return StartCoroutine(unit.MoveToLocationCo(underConstructionBuilding.transform.position));
        onWayToTask = true;

        if (carriedResource.amount == 0)
        {
            if (unit.thingInHand != null)
                unit.thingInHand.gameObject.SetActive(false);
            unit.thingInHand = transform.Find(underConstructionBuilding.GetComponent<Building>().buildingStats.toolConstructionName);
            if (unit.thingInHand != null)
                unit.thingInHand.gameObject.SetActive(true);
        }

        while (!constructionSiteReached && underConstructionBuilding != null && unit.target == underConstructionBuilding.transform.position)
            yield return null;

        onWayToTask = false;
    }

    private IEnumerator ConstructBuildingCo(GameObject underConstructionBuilding)
    {
        if (carriedResource.amount > 0)
            yield return StartCoroutine(LetDownDropResourceCo());

        StartTask();
        Vector3 underConstructionBuildingPosition = underConstructionBuilding.transform.position;
        transform.LookAt(new Vector3(underConstructionBuildingPosition.x, transform.position.y, underConstructionBuildingPosition.z));
        animator.SetTrigger("hammering");
        yield return new WaitForSeconds(0.5f); //animation transition duration

        if (underConstructionBuilding == null) // check if building was finished during animation transition
        {
            if (unit.target == underConstructionBuildingPosition)
                yield return StartCoroutine(StopTaskCo());
            yield break;
        }

        Building building = underConstructionBuilding.GetComponent<Building>();
        UnderConstruction underConstruction = underConstructionBuilding.GetComponent<UnderConstruction>();

        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(false);
        unit.thingInHand = transform.Find(building.buildingStats.toolConstructionName);
        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(true);

        while (underConstructionBuilding != null && underConstruction.BuiltPercentage() < 100f && unit.target == underConstructionBuildingPosition)
        {
            yield return null;
            underConstruction.Construct(Time.deltaTime);
            float percentageConstructedThisFrame = Time.deltaTime * 100f / building.buildingStats.constructionTime;
            building.Repair(percentageConstructedThisFrame * building.buildingStats.maxHitPoints / 100f);
        }
        if (unit.target == underConstructionBuildingPosition)
            yield return StartCoroutine(StopTaskCo());

        /*if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(false);*/
    }

    private IEnumerator CollectResourceCo(ResourceField resourceToCollect)
    {
        Vector3 targetResourcePosition = resourceToCollect.transform.position;

        if (unit.target == targetResourcePosition)
            yield break;

        ResourceRaw targetResourceType = resourceToCollect.resourceInfo.resourceRaw;

        Vector3 currentUnitTarget = unit.target;

        yield return StartCoroutine(unit.CheckIfImmobileCo());

        if (unit.target != currentUnitTarget)
            yield break; // unit target changed while waiting to be free for current task => task no longer valid

        if (carriedResource.amount > 0 && carriedResource.resourceInfo != resourceToCollect.resourceInfo)
        {
            if (unit.unitState != UnitState.WORKING)
                yield return StartCoroutine(LetDownDropResourceCo(true));
            else
                yield return StartCoroutine(LetDownDropResourceCo(false));
            // If we go from harvesting a field to targeting another (different type), we don't lift the current harvested resource, we just drop it
        }
        carriedResource.resourceInfo = resourceToCollect.resourceInfo;

        if (unit.target != currentUnitTarget)
            yield break; // unit target changed while getting ready for current task => task no longer valid

        // check to see if resource was depleted while in animation state
        if (resourceToCollect == null) 
        {
            // search for another field of same type in certain area around former field, and if not found then break
            resourceToCollect = FindResourceFieldAround(targetResourceType, targetResourcePosition);
            if (resourceToCollect != null)
                targetResourcePosition = resourceToCollect.transform.position; 
            else
                yield break;
        } 

        yield return StartCoroutine(GoToResourceCo(resourceToCollect));

        if (unit.target != targetResourcePosition) // check to see if unit target changed while walking towards res
            yield break;

        // check to see if resource was depleted while walking towards it
        if (resourceToCollect == null)
        {
            // search for another field of same type in certain area around former field, and if not found then break
            resourceToCollect = FindResourceFieldAround(targetResourceType, targetResourcePosition);
            if (resourceToCollect != null)
                CollectResource(resourceToCollect);
            yield break;
        }

        yield return StartCoroutine(HarvestResourceCo(resourceToCollect));

        if (unit.target != targetResourcePosition)
            yield break;

        if(carriedResource.amount == 0) // resource depleted while starting harvesting it
        {
            // search for another field of same type in certain area around former field, and if not found then break
            resourceToCollect = FindResourceFieldAround(targetResourceType, targetResourcePosition);
            if (resourceToCollect != null)
                CollectResource(resourceToCollect);
            yield break;
        }

        yield return StartCoroutine(StoreResourceInClosestCampCo());

        ResourceCamp campStoredInto = FindClosestResourceCampByType(ResourceManager.ResourceRawToType(resourceToCollect.resourceInfo.resourceRaw));
        if (campStoredInto != null && unit.target == campStoredInto.accessLocation)
        {
            if (resourceToCollect != null)
            {
                CollectResource(resourceToCollect);
            }
            else
            {
                // search for another field of same type in certain area around former field, and if not found then break
                resourceToCollect = FindResourceFieldAround(targetResourceType, targetResourcePosition);
                if (resourceToCollect != null)
                    CollectResource(resourceToCollect);
            }
        }
    }

    private IEnumerator GoToResourceCo(ResourceField resourceToGoTo)
    {
        yield return StartCoroutine(unit.MoveToLocationCo(resourceToGoTo.transform.position));
        onWayToTask = true;

        if (carriedResource.amount == 0)
        {
            if (unit.thingInHand != null)
                unit.thingInHand.gameObject.SetActive(false);
            unit.thingInHand = transform.Find(resourceToGoTo.resourceInfo.toolInHandName);
            if (unit.thingInHand != null)
                unit.thingInHand.gameObject.SetActive(true);
        }

        while (resourceToGoTo != null && Vector3.Distance(transform.position, resourceToGoTo.transform.position) > resourceToGoTo.collectDistance
            && unit.target == resourceToGoTo.transform.position)
            yield return null;

        onWayToTask = false;
    }

    private IEnumerator HarvestResourceCo(ResourceField resourceToHarvest)
    {
        StartTask();

        if (carriedResource.amount == 0)
            carriedResource.resourceInfo = resourceToHarvest.resourceInfo;

        transform.LookAt(new Vector3(resourceToHarvest.transform.position.x, transform.position.y, resourceToHarvest.transform.position.z));
        animator.SetTrigger(resourceToHarvest.resourceInfo.harvestAnimation);

        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(false);
        unit.thingInHand = transform.Find(resourceToHarvest.resourceInfo.toolHarvestingName);
        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(true);

        ResourceInfo resourceToHarvestInfo = resourceToHarvest.resourceInfo;
        float timeElapsed = 0f;
        while (carriedResource.amount < unit.unitStats.carryCapactity && resourceToHarvest != null
            && unit.target == resourceToHarvest.transform.position)
        {
            yield return null;
            timeElapsed += Time.deltaTime;
            if (timeElapsed > resourceToHarvest.resourceInfo.harvestTimePerUnit * unit.unitStats.harvestSpeedMultiplier)
            {
                carriedResource.amount += resourceToHarvest.HarvestResourceField();
                timeElapsed = 0f;
            }
        }

        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(false);

        if (carriedResource.amount > 0 && carriedResource.resourceInfo == resourceToHarvestInfo)
            yield return StartCoroutine(LiftResourceCo());
        else
            yield return StartCoroutine(StopTaskCo());
    }

    private IEnumerator StoreResourceCo(ResourceCamp resourceCamp, bool backToResource = false)
    {
        if (unit.target == resourceCamp.accessLocation)
            yield break;

        yield return StartCoroutine(unit.MoveToLocationCo(resourceCamp.accessLocation));

        yield return GoToCampCo(resourceCamp);

        if (unit.target == resourceCamp.accessLocation)
        {
            transform.LookAt(new Vector3(resourceCamp.transform.position.x, transform.position.y, resourceCamp.transform.position.z));

            if (carriedResource.amount == 0)
                yield break;
            if (resourceCamp.campType != ResourceType.NONE && resourceCamp.campType != carriedResource.resourceInfo.resourceType)
                yield return StartCoroutine(StoreResourceInClosestCampCo());
            else
            {
                unit.NavAgentToNavObstacle();
                yield return StartCoroutine(LetDownResourceCo());

                if (resourceCamp.campType != ResourceType.NONE && resourceCamp.campType != carriedResource.resourceInfo.resourceType)
                    DropResource(); // a check in case camp changed type during animation duration
                else
                {
                    resourceCamp.StoreResourceInCamp(carriedResource.amount, carriedResource.resourceInfo.resourceType);
                    carriedResource.amount = 0;
                }

                if (unit.target == resourceCamp.accessLocation)
                {
                    yield return StartCoroutine(StopTaskCo());

                    if (backToResource)
                    {
                        ResourceField resource = GameManager.instance.GetClosestResourceFieldOfTypeFrom(carriedResource.resourceInfo.resourceRaw, transform.position);
                        if (resource != null)
                            CollectResource(resource);
                    }
                }

                
            }
        }
    }

    private IEnumerator GoToCampCo(ResourceCamp resourceCamp)
    {
        onWayToTask = true;

        while (Vector3.Distance(transform.position, resourceCamp.accessLocation) > resourceCamp.accessDistance
            && unit.target == resourceCamp.accessLocation
            && (carriedResource.amount == 0 || resourceCamp.campType == ResourceType.NONE || resourceCamp.campType == carriedResource.resourceInfo.resourceType))
            yield return null;

        onWayToTask = false;
    }

    private IEnumerator StoreResourceInClosestCampCo()
    {
        ResourceCamp campToStoreInto = FindClosestResourceCampByType(carriedResource.resourceInfo.resourceType);
        if (campToStoreInto != null)
            yield return StartCoroutine(StoreResourceCo(campToStoreInto));
        else
        {
            yield return StartCoroutine(StopTaskCo());
            Debug.Log("No camp to store resource into.");
        }
    }

    private IEnumerator LiftResourceCo(bool instant = false)
    {
        yield return StartCoroutine(unit.CheckIfImmobileCo());

        if(!instant)
            unit.StopNavAgent();
        else
            animator.SetBool("instant", true);

        animator.SetBool(carriedResource.resourceInfo.carryAnimation, true);

        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(false);
        unit.thingInHand = transform.Find(carriedResource.resourceInfo.carriedResourceName);
        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(true);

        if (!instant)
        {
            StartCoroutine(StopTaskCo());

            unit.SetImmobile(true);
            yield return new WaitForSeconds(carriedResource.resourceInfo.liftAnimationDuration);
            unit.SetImmobile(false);
        }
        else
        {
            yield return null;
            animator.SetBool("instant", false);
        }

        unit.ChangeUnitSpeed(carriedResource.resourceInfo.carrySpeed);
    }

    private IEnumerator LetDownResourceCo(bool withAnimation = true)
    {
        yield return StartCoroutine(unit.CheckIfImmobileCo());

        unit.StopNavAgent();

        animator.SetBool(carriedResource.resourceInfo.carryAnimation, false);

        if (withAnimation)
        {
            StartCoroutine(StopTaskCo());

            unit.SetImmobile(true);
            yield return new WaitForSeconds(carriedResource.resourceInfo.dropAnimationDuration);
            unit.SetImmobile(false);
        }

        unit.ChangeUnitSpeed(UnitSpeed.RUN);

        if (unit.thingInHand != null)
            unit.thingInHand.gameObject.SetActive(false);
    }

    private IEnumerator LetDownDropResourceCo(bool withAnimation = true)
    {
        if (!withAnimation)
            DropResource();
        yield return StartCoroutine(LetDownResourceCo(withAnimation));
        if (withAnimation)
            DropResource();

    }

    private IEnumerator PickUpResourceCo(ResourceDrop resourceDrop)
    {
        if (resourceDrop == null || unit.target == resourceDrop.transform.position)
            yield break;

        if (carriedResource.amount > 0 && carriedResource.resourceInfo != resourceDrop.droppedResource.resourceInfo)
        {
            if (unit.unitState != UnitState.WORKING)
                yield return StartCoroutine(LetDownDropResourceCo(true));
            else
                yield return StartCoroutine(LetDownDropResourceCo(false));
            // If we go from harvesting a field to targeting another (different type), we don't lift the current harvested resource, we just drop it
        }
        carriedResource.resourceInfo = resourceDrop.droppedResource.resourceInfo;

        if (resourceDrop == null)
            yield break;

        yield return StartCoroutine(unit.MoveToLocationCo(resourceDrop.transform.position));

        onWayToTask = true;
        while (resourceDrop != null && Vector3.Distance(transform.position, resourceDrop.transform.position) > resourceDrop.pickupDistance
            && unit.target == resourceDrop.transform.position)
            yield return null;
        onWayToTask = false;

        if (resourceDrop == null || unit.target != resourceDrop.transform.position || carriedResource.amount == unit.unitStats.carryCapactity)
            yield break;

        if (carriedResource.amount > 0)
            yield return LetDownResourceCo();

        if (resourceDrop != null)
        {
            carriedResource.amount += resourceDrop.droppedResource.amount;
            if (carriedResource.amount > unit.unitStats.carryCapactity)
            {
                resourceDrop.droppedResource.amount = carriedResource.amount - unit.unitStats.carryCapactity;
                carriedResource.amount = unit.unitStats.carryCapactity;
            }
            else
                Destroy(resourceDrop.gameObject);
        }

        yield return StartCoroutine(LiftResourceCo());
    }

    private void DropResource()
    {
        GameObject droppedResource = Instantiate(carriedResource.resourceInfo.droppedResourcePrefab,
            new Vector3(transform.position.x, GameManager.instance.mainTerrain.SampleHeight(transform.position), transform.position.z),
            transform.rotation);
        ResourceDrop resourceDrop = droppedResource.GetComponent<ResourceDrop>();
        resourceDrop.droppedResource.amount = carriedResource.amount;
        //resourceDrop.droppedResource.resourceInfo = carriedResource.resourceInfo;
        carriedResource.amount = 0;
    }

    private void StartTask()
    {
        unit.unitState = UnitState.WORKING;
        animator.SetBool("working", true);
        unit.NavAgentToNavObstacle();
    }

    private ResourceField FindResourceFieldAround(ResourceRaw type, Vector3 searchPoint)
    {
        ResourceField newResourceToCollect = GameManager.instance.GetClosestResourceFieldOfTypeFrom(type, searchPoint);
        if (newResourceToCollect != null && Vector3.Distance(searchPoint, newResourceToCollect.transform.position) <= unit.unitStats.resourceSearchDistance)
            return newResourceToCollect;
        else
            return null;
    }

    private ResourceCamp FindClosestResourceCampByType(ResourceType searchedResourceType)
    {
        ResourceCamp closestResourceCamp = null;
        float minDistanceFromUnit = 1000f;
        float distanceFromUnit;
        foreach (ResourceCamp resourceCamp in ResourceManager.instance.resourceCamps)
        {
            if (resourceCamp.campType == ResourceType.NONE || resourceCamp.campType == searchedResourceType)
            {
                distanceFromUnit = Vector3.Distance(transform.position, resourceCamp.transform.position);
                if (distanceFromUnit < minDistanceFromUnit)
                {
                    minDistanceFromUnit = distanceFromUnit;
                    closestResourceCamp = resourceCamp;
                }
            }
        }
        return closestResourceCamp;
    }

}
