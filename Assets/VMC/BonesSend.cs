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
    private Vector3 modelPos = Vector3.zero;
    private Vector3 hipsPos = Vector3.zero;
    private Vector3 hipsPosLocal = Vector3.zero;
    private Quaternion modelRot = Quaternion.identity;
    private Quaternion hipsRot = Quaternion.identity;
    private Quaternion hipsRotLocalInv = Quaternion.identity;

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
    
    void LateUpdate()
    {
        uClient.Clear();
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
            
            modelPos = Model.transform.position;
            modelRot = Model.transform.rotation;
            if (animator) {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) {
                    hipsPos = hips.position;
                    hipsPosLocal = hips.localPosition;
                    hipsRot = hips.rotation;
                    hipsRotLocalInv = Quaternion.Inverse(hips.localRotation);
                }
            }
        }

        //モデルが更新されたときのみ読み込み
        if (Model != null && OldModel != Model)
        {
            animator = Model.GetComponent<Animator>();
            if (Model.GetComponent<NeuronAnimator>() == null)
                Model.AddComponent<NeuronAnimator>();
            blendShapeProxy = Model.GetComponent<VRMBlendShapeProxy>();
            OldModel = Model;
        }

        if (Model != null && animator != null && uClient != null)
        {
            //Root
            var RootTransform = Model.transform;
            Vector3 pos = RootTransform.position;
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
                        Vector3 position = Transform.localPosition;
                        Quaternion rotation = Transform.localRotation;

                        if (bone == HumanBodyBones.Hips) {
                            // Calculate a positional offset for the hips that includes the movement of transforms between the model root and the hips, but excludes the movement of the root
                            // First move the root back to its original position
                            Vector3 rootPos = Model.transform.position;
                            Model.transform.position = modelPos;
                            // Move the hips to its original absolute position
                            Transform.position = hipsPos;
                            // Get its local position, which will be offset by the cumulative offsets of intermediate transforms
                            Vector3 localHipsOrig = Transform.localPosition;
                            // Restore the positions of hips and root
                            Transform.localPosition = position;
                            Model.transform.position = rootPos;
                            // Subtract the local position offset
                            position -= localHipsOrig - hipsPosLocal;
                            
                            // Do the same thing for rotation
                            // First turn the root back to its original rotation
                            Quaternion rootRot = Model.transform.rotation;
                            Model.transform.rotation = modelRot;
                            // Return the hips to its original absolute rotation
                            Transform.rotation = hipsRot;
                            // Get its local rotation, which will be rotated by the cumulative rotations of intermediate transforms
                            Quaternion localHipsRotOrig = Transform.localRotation;
                            // Restore the rotations of hips and root
                            Transform.localRotation = rotation;
                            Model.transform.rotation = rootRot;
                            // Inverse rotate the local rotation difference
                            rotation = rotation * Quaternion.Inverse(localHipsRotOrig * hipsRotLocalInv);
                        }
                        
                        uClient.Send("/VMC/Ext/Bone/Pos",
                            bone.ToString(),
                            position.x, position.y, position.z,
                            rotation.x, rotation.y, rotation.z, rotation.w);
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
