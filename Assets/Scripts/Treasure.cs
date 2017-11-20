using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Treasure : MonoBehaviour
{
    [SerializeField] private int m_Value;
    [SerializeField] private Rigidbody2D m_Rigidbody;

    public int Value { get { return m_Value; } }

    public void Reject()
    {
        m_Rigidbody.AddForce(Vector2.up * 2f, ForceMode2D.Impulse);
        m_Rigidbody.AddTorque(2f);
    }
}
