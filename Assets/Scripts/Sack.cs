using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sack : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private int m_MaxCapacity = 100;
    [SerializeField] private float m_MinScale = 1f;
    [SerializeField] private float m_MaxScale = 3f;
    [SerializeField] private BoxCollider2D m_Collider;

    private Vector2 m_ModelOffsetFromParent;
    public Vector2 ModelOffset { get { return m_ModelOffsetFromParent; } }

    private int m_CurrentAmount = 0;

    public float WeightPercent { get { return m_CurrentAmount / (float)m_MaxCapacity; } }

    private void Start()
    {
        m_ModelOffsetFromParent = m_CachedTransform.localPosition;
        Scale();
    }

    public bool Fill(int amount)
    {
        int projectedAmount = m_CurrentAmount + amount;
        if (projectedAmount <= m_MaxCapacity)
        {
            m_CurrentAmount = projectedAmount;
            Scale();

            return true;
        }
        else
        {
            return false;
        }
    }

    private void Scale()
    {
        float newScale = m_MinScale + WeightPercent * (m_MaxScale - m_MinScale);

        Debug.LogFormat("percent: {0}, min: {1}, max: {2}, new scale: {3}",
            WeightPercent, m_MinScale, m_MaxScale, newScale);

        Vector3 localScale = Vector3.one;
        localScale.x = newScale;
        localScale.y = newScale;

        m_CachedTransform.localScale = localScale;
        m_Rigidbody.mass = 0.05f + (m_CurrentAmount / 5f);
    }

    public void Handle(bool holding, float direction)
    {
        m_Collider.enabled = !holding;
        m_Rigidbody.simulated = !holding;

        if (!holding)
        {
            m_Rigidbody.AddTorque(m_Rigidbody.mass * 0.65f * direction, ForceMode2D.Impulse);
        }
    }
}
