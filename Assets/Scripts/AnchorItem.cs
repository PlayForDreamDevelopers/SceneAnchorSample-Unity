using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YVR.Core;

public class AnchorItem : MonoBehaviour
{
    public YVRSpatialAnchorResult anchorResult;

    public void SetAnchorItem(YVRSpatialAnchorResult anchorResult)
    {
        this.anchorResult = anchorResult;
    }

    private void Update()
    {
        if (anchorResult.anchorHandle == 0) return;
        YVRSpatialAnchor.instance.GetSpatialAnchorPose(anchorResult.anchorHandle, out Vector3 position, out Quaternion rotation, out YVRAnchorLocationFlags locationFlags);
        transform.position = position;
        transform.rotation = rotation;
    }
}
