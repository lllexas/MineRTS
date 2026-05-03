using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct CameraProfileStateBinding
{
    public GameFlowController.GameState State;
    public CameraProfileSO ProfileSO;
}

public class CameraProfileDriver : MonoBehaviour
{
    [SerializeField] private List<CameraProfileStateBinding> _bindings = new List<CameraProfileStateBinding>
    {
        new CameraProfileStateBinding { State = GameFlowController.GameState.MainMenu, ProfileSO = null },
        new CameraProfileStateBinding { State = GameFlowController.GameState.BigMap, ProfileSO = null },
        new CameraProfileStateBinding { State = GameFlowController.GameState.InStage, ProfileSO = null }
    };

    private CameraController _cameraController;
    private Camera _targetCamera;

    public static CameraProfileDriver Instance { get; private set; }
    public CameraController CameraController => _cameraController;
    public Camera TargetCamera => _targetCamera;
    public List<CameraProfileStateBinding> Bindings => _bindings;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CameraProfileDriver] Duplicate instance detected, destroying.");
            Destroy(this);
            return;
        }

        Instance = this;
        CacheRefs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool ApplyProfileForState(GameFlowController.GameState state)
    {
        CacheRefs();

        if (!TryGetProfile(state, out CameraProfileSO profile) || profile == null)
        {
            return false;
        }

        _cameraController.ApplyProfile(profile);
        return true;
    }

    public bool CaptureProfileForState(GameFlowController.GameState state)
    {
        CacheRefs();

        if (!TryGetProfile(state, out CameraProfileSO profile) || profile == null)
        {
            return false;
        }

        profile.CaptureFrom(_targetCamera, _cameraController);
        return true;
    }

    public bool TryGetProfile(GameFlowController.GameState state, out CameraProfileSO profile)
    {
        for (int i = 0; i < _bindings.Count; i++)
        {
            if (_bindings[i].State == state)
            {
                profile = _bindings[i].ProfileSO;
                return profile != null;
            }
        }

        profile = null;
        return false;
    }

    private void CacheRefs()
    {
        if (_cameraController == null)
        {
            _cameraController = GetComponent<CameraController>();
        }

        if (_cameraController == null)
        {
            _cameraController = GetComponentInParent<CameraController>();
        }

        if (_targetCamera == null)
        {
            _targetCamera = GetComponent<Camera>();
        }

        if (_targetCamera == null)
        {
            _targetCamera = GetComponentInParent<Camera>();
        }
    }
}
