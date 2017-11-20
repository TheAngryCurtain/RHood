using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sack : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private int m_MaxCapacity = 100;
    [SerializeField] private float m_MinScale = 1f;
    [SerializeField] private float m_MaxScale = 3f;

    private int m_CurrentAmount = 0;

    private void Start()
    {
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
        float percentOfMax = m_CurrentAmount / (float)m_MaxCapacity;
        float newScale = m_MinScale + percentOfMax * (m_MaxScale - m_MinScale);

        Debug.LogFormat("percent: {0}, min: {1}, max: {2}, new scale: {3}",
            percentOfMax, m_MinScale, m_MaxScale, newScale);

        Vector3 localScale = Vector3.one;
        localScale.x = newScale;
        localScale.y = newScale;

        m_CachedTransform.localScale = localScale;
    }
}
