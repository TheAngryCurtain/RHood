using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Treasure : MonoBehaviour
{
    [SerializeField] private int m_Value;
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private float m_RejectForce = 1f;
    [SerializeField] private float m_RejectTorque = 2f;

    public int Value { get { return m_Value; } }

    public void Reject()
    {
        m_Rigidbody.AddForce(Vector2.up * m_RejectForce, ForceMode2D.Impulse);
        m_Rigidbody.AddTorque(m_RejectTorque);
    }
}
