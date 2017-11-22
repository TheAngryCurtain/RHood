using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private Transform m_TargetTransform;
    [SerializeField] private float m_LerpSpeed = 0.5f;
    [SerializeField] private float m_MinCamX = 0f;
    [SerializeField] private float m_MinCamY = 0f;

    private Vector2 m_TargetPos;

    private void LateUpdate()
    {
        if (m_TargetTransform != null)
        {
            m_TargetPos = Vector2.Lerp(m_CachedTransform.position, m_TargetTransform.position, m_LerpSpeed);
            m_TargetPos.x = Mathf.Clamp(m_TargetPos.x, m_MinCamX, 2000f);
            m_TargetPos.y = Mathf.Clamp(m_TargetPos.y, m_MinCamY, 20f);

            m_CachedTransform.position = m_TargetPos;
        }
    }
}
