using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

//container for data and simple instance implementations (enable,disable) for dynamic object
//also includes fields for initialization
//this would also include some nice functions for beginning/ending engagements

//if this is using a dynamic object id pool, will grab a new id every time 'OnEnable' is called. if this is not needed, changing that function to 'Start' should be fine

namespace CognitiveVR
{
#if CVR_VIVEWAVE
    [DefaultExecutionOrder(+10)] //this must run after PoseTrackerManager on controllers is enabled
#endif
    [HelpURL("https://docs.cognitive3d.com/unity/dynamic-objects/")]
    public class DynamicObject : MonoBehaviour
    {
        public enum CommonDynamicMesh
        {
            ViveController,
            OculusRiftTouchLeft,
            OculusRiftTouchRight,
            ViveTracker,
            ExitPoll,
            LeapMotionHandLeft,
            LeapMotionHandRight,
            WindowsMixedRealityLeft,
            WindowsMixedRealityRight,
            VideoSphereLatitude,
            VideoSphereCubemap,
            SnapdragonVRController,
            ViveFocusController, //the 6dof controller
            OculusQuestTouchLeft,
            OculusQuestTouchRight,
            PicoNeoControllerLeft,
            PicoNeoControllerRight
        }


#if UNITY_EDITOR
        //stores instanceid. used to check if something in editor has changed
        [System.NonSerialized]
        public int editorInstanceId;
        public bool HasCollider()
        {
            if (CognitiveVR_Preferences.Instance.DynamicObjectSearchInParent)
            {
                var collider = GetComponentInChildren<Collider>();
                if (collider == null)
                {
                    return false;
                }
                return true;
            }
            return true;
        }
#endif
        
        [System.NonSerialized]
        public string DataId;
        //this is only used for a custom editor to help CustomId be set correctly
        public bool UseCustomId = true;

        /// <summary>
        /// should use GetId() to get the currently assigned dynamic object id
        /// </summary>
        public string CustomId;
        public float UpdateRate = 0.1f;


        public bool UseCustomMesh = true;
        public string MeshName;
        public CommonDynamicMesh CommonMesh;


        public float PositionThreshold = 0.01f;
        public float RotationThreshold = 0.1f;
        public float ScaleThreshold = 0.1f;

        public bool TrackBoneRotations;
        public Transform[] TrackedBones;

        public bool IsController;
        public bool IsRight;
        public string ControllerType;

        public DynamicObjectIdPool IdPool;

        [System.NonSerialized]
        public Vector3 StartingScale;

        bool IsMedia
        {
            get
            {
                return GetComponent<MediaComponent>() != null;
            }
        }
        public string mediaURL;

        //make this dynamic object record position on the same frame as physics gaze
        public bool SyncWithPlayerGazeTick;

#if CVR_VIVEWAVE
        bool hasCompletedDelay = false;
        IEnumerator Start()
        {
            //vive wave controller loader spawns a prefab (which calls enable) before setting correct values
            if (!IsController) { yield break; }
            if (hasCompletedDelay) { yield break; }
            yield return null;
            hasCompletedDelay = true;
            OnEnable();
        }
#endif
        

        private void OnEnable()
        {
#if CVR_VIVEWAVE
            if (IsController && !hasCompletedDelay)
                return;
#endif
            StartingScale = transform.lossyScale;
            if (CognitiveVR.Core.IsInitialized)
            {                
                string tempMeshName = UseCustomMesh ? MeshName : CommonMesh.ToString().ToLower();

                if (!UseCustomMesh && CommonMesh == CommonDynamicMesh.WindowsMixedRealityRight)
                    tempMeshName = "windows_mixed_reality_controller_right";
                if (!UseCustomMesh && CommonMesh == CommonDynamicMesh.WindowsMixedRealityLeft)
                    tempMeshName = "windows_mixed_reality_controller_left";

                if (SyncWithPlayerGazeTick)
                {
                    UpdateRate = 64;
                }

                string registerid = UseCustomId ? CustomId : "";

                if (!UseCustomId && IdPool != null)
                {
                    UseCustomId = true;
                    CustomId = IdPool.GetId();
                    registerid = CustomId;
                }

                var Data = new DynamicData(gameObject.name, registerid, tempMeshName, transform, transform.position, transform.rotation, transform.lossyScale, PositionThreshold, RotationThreshold, ScaleThreshold, UpdateRate, IsController, ControllerType,IsRight,TrackBoneRotations);

                DataId = Data.Id;

                if (IsController)
                {
#if CVR_VIVEWAVE
                    var devicetype = GetComponent<WaveVR_PoseTrackerManager>().Type;
                    if (WaveVR_Controller.Input(devicetype).DeviceType == wvr.WVR_DeviceType.WVR_DeviceType_Controller_Left)
                    {
                        Data.IsRightHand = false;
                    }
                    else
                    {
                        Data.IsRightHand = true;
                    }
                    CognitiveVR.GameplayReferences.SetController(gameObject, Data.IsRightHand);
#endif
#if CVR_WINDOWSMR
                    CognitiveVR.GameplayReferences.SetController(gameObject, IsRight);
#endif
                    CognitiveVR.DynamicManager.RegisterController(Data);
                }
                else if (IsMedia)
                {
                    DynamicManager.RegisterMedia(Data, mediaURL);
                }
                else
                {
                    CognitiveVR.DynamicManager.RegisterDynamicObject(Data);
                }
                if (TrackBoneRotations)
                {
                    rotations = new Dictionary<string, Quaternion>(TrackedBones.Length);
                    for(int i = 0; i<TrackedBones.Length;i++)
                    {
                        rotations.Add(TrackedBones[i].name, new Quaternion(0,0,0,0));
                    }
                }
                if (SyncWithPlayerGazeTick || TrackBoneRotations)
                {
                    CognitiveVR.Core.TickEvent += Core_TickEvent;
                }
            }
            else
            {
                CognitiveVR.Core.InitEvent += OnCoreInitialize;
            }
        }

        Dictionary<string, Quaternion> rotations;
        private void Core_TickEvent()
        {
            if (TrackBoneRotations)
            {
                bool writeAnyData = false;
                var properties = new List < KeyValuePair<string, object>>();
                //iterate on all bones and write as properties
                for(int i = 0; i<TrackedBones.Length;i++)
                {
                    //TODO check if rotation beyond a threshold
                    //rotations[TrackedBones[i].name]
                    var rot = TrackedBones[i].localRotation;
                    float f = Quaternion.Dot(rotations[TrackedBones[i].name], rot);

                    float fabs = f < 0 ? f * -1 : f;
                    float min = fabs < 1 ? fabs : 1;

                    bool writeData = false;
                    if (System.Math.Acos(min) * 114.59156f > RotationThreshold)
                    {
                        rotations[TrackedBones[i].name] = rot;
                        //ActiveDynamicObjectsArray[index].dirty = true;
                        writeData = true;
                        writeAnyData = true;
                    }
                    if (writeData)
                        properties.Add(new KeyValuePair<string, object>(TrackedBones[i].name, TrackedBones[i].localRotation));
                }
                if (writeAnyData)
                    RecordSnapshot(properties);
            }
            else
            {
                CognitiveVR.DynamicManager.RecordDynamic(DataId, false);
            }
        }

        private void OnCoreInitialize(CognitiveVR.Error error)
        {
            CognitiveVR.Core.InitEvent -= OnCoreInitialize;
            OnEnable();
        }

        /// <summary>
        /// returns the Id of the Dynamic Object
        /// </summary>
        /// <returns></returns>
        public string GetId()
        {
            if (DynamicManager.IsDataActive(DataId))
                return DataId;
            if (!string.IsNullOrEmpty(CustomId))
                return CustomId;
            return string.Empty;
        }

        /// <summary>
        /// manually record position and rotation on this dynamic object
        /// </summary>
        public void RecordSnapshot()
        {
            DynamicManager.SetDirty(DataId);
        }

        /// <summary>
        /// manually record position and rotation and properties on this dynamic object
        /// </summary>
        public void RecordSnapshot(List<KeyValuePair<string, object>> properties)
        {
            DynamicManager.SetProperties(DataId,properties);
        }

        public void RecordSnapshot(Dictionary<string,object> properties)
        {
            List<KeyValuePair<string, object>> temp = new List<KeyValuePair<string, object>>(properties.Count);
            foreach(var prop in properties)
            {
                temp.Add(new KeyValuePair<string, object>(prop.Key, prop.Value));
            }
            DynamicManager.SetProperties(DataId, temp);
        }

        /// <summary>
        /// Alternate method for beginning a Custom Event and setting this Dynamic Object as the target
        /// </summary>
        /// <param name="engagementName">name of the event</param>
        /// <param name="uniqueEngagementId">if multiple events with the same name are expected on this object, this can be used to end specific events</param>
        /// <param name="properties">optional parameters to add to the custom event</param>
        public void BeginEngagement(string engagementName, string uniqueEngagementId = null, List<KeyValuePair<string,object>> properties = null)
        {
            DynamicManager.BeginEngagement(GetId(), engagementName, uniqueEngagementId, properties);
        }

        /// <summary>
        /// Alternate method to end a Custom Event on a specific Dynamic Object. If the event does not exist, creates and immediately ends the event
        /// </summary>
        /// <param name="engagementName">the name of the Event to end</param>
        /// <param name="uniqueEngagementId">identifies the event to end, regardless of name</param>
        /// <param name="properties">any properties to add to this event before it ends</param>
        public void EndEngagement(string engagementName, string uniqueEngagementId = null, List<KeyValuePair<string, object>> properties = null)
        {
            DynamicManager.EndEngagement(GetId(), engagementName, uniqueEngagementId, properties);
        }

        private void OnDisable()
        {
            CognitiveVR.Core.InitEvent -= OnCoreInitialize;
            CognitiveVR.Core.TickEvent -= Core_TickEvent;

            DynamicManager.SetTransform(DataId, transform);

            CognitiveVR.DynamicManager.RemoveDynamicObject(DataId);
        }


#if UNITY_EDITOR
    private void Reset()
    {
        //set name is not set otherwise
        if (string.IsNullOrEmpty(MeshName))
        {
            MeshName = gameObject.name.ToLower().Replace(" ", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_").Replace("?", "_").Replace("*", "_").Replace("\"", "_").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }

        //set custom id if not set otherwise
        if (string.IsNullOrEmpty(CustomId))
        {
            string s = System.Guid.NewGuid().ToString();
            CustomId = "editor_" + s;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
    }
#endif
    }
}