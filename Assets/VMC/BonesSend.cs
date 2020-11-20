/*
 * SampleBonesSend
 * gpsnmeajp
 * https://sh-akira.github.io/VirtualMotionCaptureProtocol/
 *
 * These codes are licensed under CC0.
 * http://creativecommons.org/publicdomain/zero/1.0/deed.ja
 */
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VRM;

[RequireComponent(typeof(uOSC.uOscClient))]
public class BonesSend : MonoBehaviour
{
    uOSC.uOscClient uClient = null;

    public GameObject Model = null;
    public Text error;
    private GameObject OldModel = null;
    private Transform hips = null;
    private Vector3 hipsPosition = Vector3.zero;
    private Vector3 hipsPositionLocal = Vector3.zero;

    Animator animator = null;
    VRMBlendShapeProxy blendShapeProxy = null;

    public enum VirtualDevice
    {
        HMD = 0,
        Controller = 1,
        Tracker = 2,
    }

    void Start()
    {
        uClient = GetComponent<uOSC.uOscClient>();
    }
    
    void Update()
    {
        if (Model == null) {
            Animator[] avatars = FindObjectsOfType<Animator>();
            if (avatars.Length > 1) {
                if (error != null)
                    error.text = "Error: Please only put one avatar into the scene.";
                return;
            } else if (avatars.Length == 0) {
                if (error != null)
                    error.text = "Error: Please put exactly one avatar into the scene.";
                return;
            }
            if (error != null)
                error.text = null;
            Model = avatars[0].gameObject;
        }

        //モデルが更新されたときのみ読み込み
        if (Model != null && OldModel != Model)
        {
            animator = Model.GetComponent<Animator>();
            blendShapeProxy = Model.GetComponent<VRMBlendShapeProxy>();
            OldModel = Model;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) {
                hipsPosition = hips.position;
                hipsPositionLocal = hips.localPosition;
            }
        }

        if (Model != null && animator != null && uClient != null)
        {
            //Root
            var RootTransform = Model.transform;
            Vector3 pos = RootTransform.position;
            if (hips != null)
                pos += hips.position - hipsPosition;
            if (RootTransform != null)
            {
                uClient.Send("/VMC/Ext/Root/Pos",
                    "root",
                    pos.x, pos.y, pos.z,
                    RootTransform.rotation.x, RootTransform.rotation.y, RootTransform.rotation.z, RootTransform.rotation.w);
            }

            //Bones
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone != HumanBodyBones.LastBone)
                {
                    var Transform = animator.GetBoneTransform(bone);
                    if (Transform != null)
                    {
                        pos = Transform.localPosition;
                        if (bone == HumanBodyBones.Hips) {
                            pos = hipsPositionLocal;
                        }
                        uClient.Send("/VMC/Ext/Bone/Pos",
                            bone.ToString(),
                            pos.x, pos.y, pos.z,
                            Transform.localRotation.x, Transform.localRotation.y, Transform.localRotation.z, Transform.localRotation.w);
                    }
                }
            }

            //ボーン位置を仮想トラッカーとして送信
            SendBoneTransformForTracker(HumanBodyBones.Head, "Head");
            SendBoneTransformForTracker(HumanBodyBones.Spine, "Spine");
            SendBoneTransformForTracker(HumanBodyBones.LeftHand, "LeftHand");
            SendBoneTransformForTracker(HumanBodyBones.RightHand, "RightHand");
            SendBoneTransformForTracker(HumanBodyBones.LeftFoot, "LeftFoot");
            SendBoneTransformForTracker(HumanBodyBones.RightFoot, "RightFoot");

            //BlendShape
            if (blendShapeProxy != null)
            {
                foreach (var b in blendShapeProxy.GetValues())
                {
                    uClient.Send("/VMC/Ext/Blend/Val",
                        b.Key.ToString(),
                        (float)b.Value
                        );
                }
                uClient.Send("/VMC/Ext/Blend/Apply");
            }

            //Available
            uClient.Send("/VMC/Ext/OK", 1);
        }
        else
        {
            uClient.Send("/VMC/Ext/OK", 0);
        }
        uClient.Send("/VMC/Ext/T", Time.time);
    }

    void SendBoneTransformForTracker(HumanBodyBones bone, string DeviceSerial)
    {
        var DeviceTransform = animator.GetBoneTransform(bone);
        if (DeviceTransform != null) {
            uClient.Send("/VMC/Ext/Tra/Pos",
        (string)DeviceSerial,
        (float)DeviceTransform.position.x,
        (float)DeviceTransform.position.y,
        (float)DeviceTransform.position.z,
        (float)DeviceTransform.rotation.x,
        (float)DeviceTransform.rotation.y,
        (float)DeviceTransform.rotation.z,
        (float)DeviceTransform.rotation.w);
        }
    }
}
