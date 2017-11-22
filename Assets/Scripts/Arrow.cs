﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private TrailRenderer m_Trail;
    [SerializeField] private LineRenderer m_RopeRenderer;

    private bool m_Launched = false;
    private float m_PrevRotation;
    private Vector2 m_Velocity;
    private System.Action<Rigidbody2D> m_GrappleCallback;
    private Transform m_Owner;

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

        // update rope
        if (m_GrappleCallback != null)
        {
            m_RopeRenderer.positionCount = 2;
            m_RopeRenderer.SetPosition(0, m_Owner.position);
            m_RopeRenderer.SetPosition(1, m_CachedTransform.position);
        }
    }

    public void Launch(Vector2 direction, float power, Transform playerTransform, System.Action<Rigidbody2D> callback)
    {
        m_Owner = playerTransform;
        m_Rigidbody.AddForce(direction * power, ForceMode2D.Impulse);
        m_Launched = true;
        m_Trail.enabled = true;

        if (callback != null)
        {
            m_GrappleCallback = callback;
            m_RopeRenderer.enabled = true;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Rope"))
        {
            // cut the rope
            if (m_Rigidbody.velocity.magnitude > 5f)
            {
                HingeJoint2D joint = collision.gameObject.GetComponent<HingeJoint2D>();
                if (joint != null)
                {
                    joint.connectedBody = null;
                    joint.enabled = false;
                }
            }
        }
        else
        {
            //Debug.Log(m_Velocity.magnitude);
            m_Launched = false;
            m_Trail.enabled = false;

            m_Rigidbody.velocity = Vector3.zero;
            m_Rigidbody.angularVelocity = 0f;
            m_Rigidbody.isKinematic = true;

            // restore the last rotation before the collision
            m_Rigidbody.MoveRotation(m_PrevRotation);
            m_CachedTransform.SetParent(collision.transform);

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

    public void CancelGrapple()
    {
        m_RopeRenderer.enabled = false;
        m_GrappleCallback = null;
    }

    public void BreakGrapple()
    {
        m_RopeRenderer.enabled = false;
        Destroy(this.gameObject, 5f);
    }
}
