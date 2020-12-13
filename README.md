# VSeeFacePreview

This repository contains a simple example Unity 2019.4.16f1 project for previewing models for use in VSeeFace.

The provided scene inside the Assets\Scenes folder will automatically look for a VRM model inside the scene when play mode is enabled. The pose of this model will be streamed using the VMC protocol and can be received by VSeeFace v1.13.34b and later, if the corresponding option in the general settings is enabled. Note that only a single VRM model should be placed inside the scene.