using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sack : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private int m_MaxCapacity = 100;

    private int m_CurrentAmount = 0;

    public bool Fill(int amount)
    {
        Debug.LogFormat("amount: {0}, total: {1}", amount, m_CurrentAmount);

        int projectedAmount = m_CurrentAmount + amount;
        if (projectedAmount <= m_MaxCapacity)
        {
            m_CurrentAmount = projectedAmount;
            return true;
        }
        else
        {
            return false;
        }
    }
}
