﻿using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif
using System;

using System.Collections.Generic;

using LockingPolicy = Thalmic.Myo.LockingPolicy;

// Allows access to one or more Myo armbands, which must be immediate children of the GameObject this script is attached
// to. ThalmicHub is a singleton; only one ThalmicHub instance is allowed in a scene, which can be accessed through
// ThalmicHub.instance. ThalmicHub will persist across scene changes.
public class ThalmicHub : MonoBehaviour
{
    // The single instance of ThalmicHub. Set during Awake.
    public static ThalmicHub instance {
        get { return _instance; }
    }

#if UNITY_ANDROID
    private static AndroidJavaObject jo;
#endif

    // Unique application identifier. applicationIdentifier must follow a reverse domain name format
    // (ex. com.domainname.appname). Application identifiers can be formed from the set of alphanumeric ASCII characters
    // (a-z, A-Z, 0-9). The hyphen (-) and underscore (_) characters are permitted if they are not adjacent to a period
    // (.) character  (i.e. not at the start or end of each segment), but are not permitted in the top-level domain.
    // Application identifiers must have three or more segments. For example, if a company's domain is example.com and
    // the application is named hello-world, one could use "com.example.hello-world" as a valid application identifier.
    // applicationIdentifier can an empty string.
    public string applicationIdentifier = "com.example.myo-unity";

    // If set to None, pose events are always sent. If set to Standard, pose events are not sent while a Myo is locked.
    public LockingPolicy lockingPolicy;

    // True if and only if the hub initialized successfully; typically this is set during Awake, but it can also be
    // set by calling ResetHub() explicitly. The typical reason for initialization to fail is that Myo Connect is not
    // running.
    public bool hubInitialized {
        get { return _hub != null; }
    }

    // Reset the hub. This function is typically used if initialization failed to attempt to initialize again (e.g.
    // after asking the user to ensure that Myo Connect is running).
    public bool ResetHub() {
        if (_hub != null) {

//#if UNITY_EDITOR || !UNITY_IPHONE
#if UNITY_EDITOR
            _hub.Dispose ();
            _hub = null;
#endif
            foreach (ThalmicMyo myo in _myos) {
                myo.internalMyo = null;
				myo.identifier = null;
            }
        }
//#if UNITY_ANDROID && !UNITY_EDITOR
        
//#else
        return createHub();
//#endif
    }

    void Awake ()
    {
        // Ensure that there is only one ThalmicHub.
        if (_instance != null) {
#if UNITY_EDITOR
            EditorUtility.DisplayDialog("Can only have one ThalmicHub",
                                        "Your scene contains more than one ThalmicHub. Remove all but one ThalmicHub.",
                                        "OK");
#endif
            Destroy (this.gameObject);
            return;
        } else {
            _instance = this;
        }

        // Do not destroy this game object. This will ensure that it remains active even when
        // switching scenes.
        DontDestroyOnLoad(this);
	
		//if we are running on iOS initialize the iOSManager to receive updates to myo objects
		if (Application.platform == RuntimePlatform.IPhonePlayer)
		{
			GameObject go = new GameObject("MyoIOSManager");
			go.AddComponent<MyoIOSManager>();
			go.transform.parent = this.transform;
		}
        if (Application.platform == RuntimePlatform.Android)
        {
            GameObject go = new GameObject("MyoAndroidManager");
            go.AddComponent<MyoAndroidManager>();
            go.transform.parent = this.transform;
        }

        for (int i = 0; i < transform.childCount; ++i) {
            Transform child = transform.GetChild (i);

            var myo = child.gameObject.GetComponent<ThalmicMyo> ();
            if (myo != null) {
                _myos.Add(myo);
            }
        }

        if (_myos.Count < 1) {
            string errorMessage = "The ThalmicHub's GameObject must have at least one child with a ThalmicMyo component.";
#if UNITY_EDITOR
            EditorUtility.DisplayDialog ("Thalmic Hub has no Myo children", errorMessage, "OK");
#else
            throw new UnityException (errorMessage);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        jo = new AndroidJavaObject ("com.strieg.myoplugin.MyoStarter");
			using (AndroidJavaClass ajc = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
				using (AndroidJavaObject ajo = ajc.GetStatic<AndroidJavaObject>("currentActivity")) {
					jo.Call ("launchService", ajo);
				}
			}
#else
        createHub();
#endif
    }

    private bool createHub () {

//#if UNITY_EDITOR || !UNITY_IPHONE
#if UNITY_EDITOR
        try
        {
            _hub = new Thalmic.Myo.Hub (applicationIdentifier, hub_MyoPaired);

            _hub.SetLockingPolicy (lockingPolicy);
        } catch (System.Exception e) {
            Debug.Log ("ThalmicHub failed to initialize." + e.ToString());
            return false;
        }
#endif
        return true;
    }

    void OnApplicationQuit ()
    {
        if (_hub != null) {
//#if UNITY_EDITOR || !UNITY_IOS
#if UNITY_EDITOR
            _hub.Dispose ();
            _hub = null;
#endif
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        using (AndroidJavaClass ajc = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            using (AndroidJavaObject ajo = ajc.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                jo.Call("stopService", ajo);
            }
        }
        jo = null;
#endif
    }

    void hub_MyoPaired (object sender, Thalmic.Myo.MyoEventArgs e)
    {
        foreach (ThalmicMyo myo in _myos) {
            if (myo.internalMyo == null) {
                myo.internalMyo = e.Myo;
                break;
            }
        }
    }

    public static void AttachToAdjacent()
    {
#if UNITY_ANDROID && !UNITY_EDITOR  
        jo.Call ("attachToAdjacent");
#endif
    }


//Add the Mac Address of your Myo Device here
    public static void AttachByMacAddress()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        jo.Call ("attachByMacAddress", "ED:AD:99:93:E2:90");
#endif
    }

    public static void VibrateForLength(int length)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
		jo.Call ("vibrateForLength", length);
#endif
    }

    private static ThalmicHub _instance = null;

    private Thalmic.Myo.Hub _hub = null;

    public List<ThalmicMyo> _myos = new List<ThalmicMyo>();
	
	//Events
	public static Action <ThalmicMyo, Vector3> DidUpdateAccelerometerData;
	public static Action <ThalmicMyo, Quaternion> DidUpdateOrientationData;
	public static Action <ThalmicMyo, Thalmic.Myo.Pose> DidReceivePoseChange;
	public static Action <ThalmicMyo, float[]> DidReceiveEmgData;
	public static Action <ThalmicMyo> DidConnectDevice;
	public static Action <ThalmicMyo> DidDisconnectDevice;
	public static Action <ThalmicMyo> DidUnlockDevice;
	public static Action <ThalmicMyo> DidLockDevice;
	public static Action <ThalmicMyo> DidSyncArm;
	public static Action <ThalmicMyo> DidUnsyncArm;
}
