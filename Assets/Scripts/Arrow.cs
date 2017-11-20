using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private TrailRenderer m_Trail;
    [SerializeField] private float m_MinStickSpeed = 2f;

    private bool m_Launched = false;
    private float m_PrevRotation;
    private Vector2 m_Velocity;
    private System.Action<Rigidbody2D> m_GrappleCallback;

    private void FixedUpdate()
    {
        if (m_Launched)
        {
            // update rotation based on velocity
            m_PrevRotation = m_Rigidbody.rotation;

            m_Velocity = m_Rigidbody.velocity;
            float angle = Mathf.Atan2(m_Velocity.y, m_Velocity.x) * Mathf.Rad2Deg;
            m_Rigidbody.MoveRotation(angle);
        }
    }

    public void Launch(Vector2 direction, float power, System.Action<Rigidbody2D> callback)
    {
        m_Rigidbody.AddForce(direction * power, ForceMode2D.Impulse);
        m_Launched = true;
        m_Trail.enabled = true;

        if (callback != null)
        {
            m_GrappleCallback = callback;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log(m_Velocity.magnitude);
        if (m_Velocity.magnitude > m_MinStickSpeed)
        {
            m_Launched = false;
            m_Trail.enabled = false;

            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.angularVelocity = 0f;
            m_Rigidbody.isKinematic = true;

            // restore the last rotation before the collision
            m_Rigidbody.MoveRotation(m_PrevRotation);

            if (m_GrappleCallback != null)
            {
                m_GrappleCallback(m_Rigidbody);
            }
            else
            {
                // TODO change this to when arrows go offscreen
                Destroy(this.gameObject, 5f);
            }
        }
    }
}
