using UnityEngine;
using KSP;
using KSP.Game;
using SpaceWarp;
using SpaceWarp.UI;
using SpaceWarp.API.UI.Appbar;
using SpaceWarp.API.Mods;
using BepInEx;

namespace EloxKerbalview
{
    [BepInPlugin("com.Elox.EloxKerbalView", "EloxKerbalView", "1.1.0")]
    [BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
    public class EloxKerbalviewMod : BaseSpaceWarpPlugin
    {
        private static EloxKerbalviewMod Instance { get; set; }
        static bool loaded = false;
        static bool firstPersonEnabled = false;

        KSP.Sim.impl.VesselComponent kerbal = null;
        KSP.Sim.impl.VesselBehavior kerbalBehavior = null;
        float lastKerbalYRotation;

        Camera currentCamera;
        Camera skyCamera;
        Camera scaledCamera;
        Vector3 savedPosition;
        Quaternion savedRotation;
        Transform savedParent;

        static float cameraNearClipPlane = 8;
        static float cameraFOV = 90;
        static float cameraForwardOffset = 10;
        static float cameraUpOffset = 12;

        float savedFov;
        float savedNearClip;

        static GameObject helmetLights;
        KSP.Sim.Definitions.ModuleAction toggleLightsAction;
        static float range = 20, spotAngle = 45, lightIntesity = 100;

        public override void OnInitialized() {
            Logger.LogInfo("KerbalView is initialized");

            if (loaded) {
                Destroy(this);
            }
        }

        public override void OnPostInitialized()
        {
            loaded = true; 
        }
        void Awake() {
            firstPersonEnabled = false;
            toggleLightsAction = new KSP.Sim.Definitions.ModuleAction((Delegate)toggleHelmetLights);
        }

        void Update() {

            if (loaded) { 
            GameStateConfiguration gameStateConfiguration = GameManager.Instance.Game.GlobalGameState.GetGameState();
         
            if (gameStateConfiguration.IsFlightMode){
                if (kerbalBehavior != null) {
                    if (isFirstPersonViewEnabled() && gameChangedCamera()) disableFirstPerson();
                    if (isFirstPersonViewEnabled()) updateStars();

                    if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.L)) toggleHelmetLights();
                    if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha2) && GameManager.Instance.Game.CameraManager.FlightCamera.Mode == KSP.Sim.CameraMode.Auto) {
                        if (!isFirstPersonViewEnabled()) {
                            enableFirstPerson();
                        } else {
                            disableFirstPerson();
                        }
                    }
                } else {
                    findKerbal();
                }
            }
            }
        }

        void toggleHelmetLights() {
            if (helmetLights) {
                Destroy(helmetLights);
            } else if (kerbalBehavior) {
                helmetLights = new GameObject("EVA_HelmetLight");
                GameObject helmetLightLeft = new GameObject("EVA_HelmetLightLeft");
                GameObject helmetLightRight = new GameObject("EVA_HelmetLightRight");

                helmetLights.transform.parent = kerbalBehavior.transform;
                helmetLightLeft.transform.parent = helmetLights.transform;
                helmetLightRight.transform.parent = helmetLights.transform;

                helmetLights.transform.localPosition = new Vector3(0, 0.12f, 0.1f);
                helmetLightLeft.transform.localPosition = new Vector3(0.3f, 0, 0);
                helmetLightRight.transform.localPosition = new Vector3(-0.3f, 0, 0);

                helmetLights.transform.localRotation = Quaternion.identity;
                helmetLightLeft.transform.localEulerAngles = new Vector3(8, 5, 0);
                helmetLightRight.transform.localEulerAngles = new Vector3(8, -5, 0);

                Light insideLight = helmetLights.AddComponent<Light>();
                Light lightCompLeft = helmetLightLeft.AddComponent<Light>();
                Light lightCompRight = helmetLightRight.AddComponent<Light>();

                lightCompLeft.type = LightType.Spot;
                lightCompRight.type = LightType.Spot;

                lightCompLeft.color = Color.white;
                lightCompLeft.range = range;
                lightCompLeft.spotAngle = spotAngle;
                lightCompLeft.intensity = 0.01f * lightIntesity;

                lightCompRight.color = Color.white;
                lightCompRight.range = range;
                lightCompRight.spotAngle = spotAngle;
                lightCompRight.intensity = 0.01f * lightIntesity;

                insideLight.color = Color.white;
                insideLight.intensity = 2;
                insideLight.range = 0.5f;
            }
        }

        void updateStars() {
            var movement = currentCamera.transform.rotation.eulerAngles.y - lastKerbalYRotation;
            lastKerbalYRotation = currentCamera.transform.rotation.eulerAngles.y;

            var targetY = skyCamera.transform.eulerAngles.y + movement;
            
            skyCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, targetY, currentCamera.transform.eulerAngles.z);
            scaledCamera.transform.eulerAngles = new Vector3(currentCamera.transform.eulerAngles.x, targetY, currentCamera.transform.eulerAngles.z);
        }

        bool gameChangedCamera() {
            return currentCamera != Camera.main || GameManager.Instance.Game.CameraManager.FlightCamera.Mode != KSP.Sim.CameraMode.Auto || GameManager.Instance.Game.ViewController.GetActiveSimVessel() != kerbal;
        }
        
        void enableFirstPerson() {
            // Take control of the camera
            GameManager.Instance.Game.CameraManager.DisableInput();

            try {
                currentCamera = Camera.main;

                // Get SkyBox and Scaled camera
                foreach (Camera c in Camera.allCameras) {
                    if (c.gameObject.name == "FlightCameraSkybox_Main") {
                        skyCamera = c;
                    } else if (c.gameObject.name == "FlightCameraScaled_Main") { 
                        scaledCamera = c;
                    }
                }

                // Save config
                savedParent = currentCamera.transform.parent;
                savedRotation = currentCamera.transform.localRotation;
                savedPosition = currentCamera.transform.localPosition;

                savedFov = currentCamera.fieldOfView;
                savedNearClip = currentCamera.nearClipPlane;

                // Camera config
                currentCamera.fieldOfView = cameraFOV;
                currentCamera.nearClipPlane = 0.01f*cameraNearClipPlane;

                // Current sky deviation caused by time
                var time = skyCamera.transform.eulerAngles.y - currentCamera.transform.eulerAngles.y;

                // Anchor camera to our little friend
                currentCamera.transform.parent = kerbalBehavior.transform;
                currentCamera.transform.localRotation = Quaternion.identity;
                var targetPosition = kerbalBehavior.transform.position + 0.01f*cameraUpOffset*kerbalBehavior.transform.up + 0.01f*cameraForwardOffset*kerbalBehavior.transform.forward;
                currentCamera.transform.position = targetPosition;
                
                // Sync cameras and desync by time
                skyCamera.transform.rotation = currentCamera.transform.rotation;
                scaledCamera.transform.rotation = currentCamera.transform.rotation;
                skyCamera.transform.eulerAngles += new Vector3(0,time,0);
                scaledCamera.transform.eulerAngles += new Vector3(0,time,0);

                lastKerbalYRotation = currentCamera.transform.rotation.eulerAngles.y;

                firstPersonEnabled = true;
            } catch (Exception exception) {
                // For unknown error cases
                Logger.LogError(exception.Message);
                GameManager.Instance.Game.CameraManager.EnableInput();
            }

            
        }

        void disableFirstPerson() {
            // To avoid NullRefs
            if (currentCamera && skyCamera && scaledCamera) {
                var time = skyCamera.transform.eulerAngles.y - currentCamera.transform.eulerAngles.y;
                
                // Revert changes
                currentCamera.transform.parent = savedParent;
                currentCamera.transform.localPosition = savedPosition;
                currentCamera.transform.localRotation = savedRotation;

                // Sync cameras and desync by time
                skyCamera.transform.rotation = currentCamera.transform.rotation;
                scaledCamera.transform.rotation = currentCamera.transform.rotation;
                skyCamera.transform.eulerAngles += new Vector3(0,time,0);
                scaledCamera.transform.eulerAngles += new Vector3(0,time,0);

                // Reset local rotations (tends to variate a bit with movement)
                skyCamera.transform.localRotation = Quaternion.identity;
                scaledCamera.transform.localRotation = Quaternion.identity;

                currentCamera.nearClipPlane = savedNearClip;
                currentCamera.fieldOfView = savedFov;
            }
            
            GameManager.Instance.Game.CameraManager.EnableInput();

            kerbal = null;
            kerbalBehavior = null;

            firstPersonEnabled = false;
        }

        bool isFirstPersonViewEnabled() {
            return firstPersonEnabled;
        }

        bool findKerbal() {
            var activeVessel = GameManager.Instance.Game.ViewController.GetActiveSimVessel();
            kerbal = (activeVessel != null && activeVessel.IsKerbalEVA )? activeVessel : null;
            if (kerbal != null) {
                kerbalBehavior = GameManager.Instance.Game.ViewController.GetBehaviorIfLoaded(kerbal);
                kerbal.SimulationObject.Kerbal.KerbalData.AddAction("Toggle Helmet Lights", toggleLightsAction);
            }
            
            return kerbalBehavior != null;
        }


    }
}

