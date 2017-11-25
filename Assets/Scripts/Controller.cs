using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller : MonoBehaviour
{
    [SerializeField] protected Transform m_CachedTransform;
    [SerializeField] protected Rigidbody2D m_Rigidbody;
    [SerializeField] protected Transform m_ModelContainer;
    [SerializeField] protected GameObject m_ArrowPrefab;
    [SerializeField] protected Collider2D m_Collider;

    [SerializeField] protected float m_DefaultMoveSpeed = 5f;
    [SerializeField] protected float m_MaxJumpVelocity = 6f;
    [SerializeField] protected float m_MinJumpVelocity = 2f;
    [SerializeField] protected float m_AccelerationGrounded = 0.5f;
    [SerializeField] protected float m_AccelerationAirborne = 0.75f;
    [SerializeField] protected float m_ShootForce = 5f;

    protected bool m_TouchingJumpableSurface { get { return m_SurfaceBelow || m_SurfaceLeft || m_SurfaceRight; } }

    // this is a shared bool that should be set by a child class before actually jumping
    protected bool m_CanJump = false;
    protected GameObject m_ActiveArrowObj;

    protected bool m_SurfaceBelow = false;
    protected bool m_SurfaceLeft = false;
    protected bool m_SurfaceRight = false;
    protected bool m_SurfaceAbove = false;

    protected float m_MovementX;
    protected float m_VelXSmooth;
    protected bool m_RequestJump = false;
    protected float m_FacingDirection;

    protected void Move(Vector2 velocity)
    {
        float direction = Mathf.Sign(m_MovementX);
        if (m_MovementX != 0f)
        {
            m_FacingDirection = direction;
        }

        FlipSprite(m_FacingDirection);
        m_Rigidbody.velocity = velocity;
    }

    protected void Jump(Vector3 velocity)
    {
        if (m_CanJump && m_RequestJump)
        {
            m_Rigidbody.velocity = velocity;
            m_RequestJump = false;
        }
    }

    protected Arrow Shoot(Vector3 direction, System.Action ArrowLandedCallback)
    {
        GameObject arrowObj = (GameObject)Instantiate(m_ActiveArrowObj, null);
        arrowObj.transform.position = m_CachedTransform.position;
        Arrow arrow = arrowObj.GetComponent<Arrow>();
        if (arrow != null)
        {
            arrow.Launch(direction, m_ShootForce, m_CachedTransform, ArrowLandedCallback);

            // push player back
            m_Rigidbody.AddForce(-direction * 2f, ForceMode2D.Impulse);
        }

        return arrow;
    }

    protected void FlipSprite(float direction)
    {
        Vector3 localScale = m_ModelContainer.localScale;
        localScale.x = (direction < 0f ? -1 : 1);
        m_ModelContainer.localScale = localScale;
    }

    public void HandleTriggerCollision(eCollisionZone zone, bool enter)
    {
        switch (zone)
        {
            case eCollisionZone.Below:
                m_SurfaceBelow = enter;
                break;

            case eCollisionZone.Above:
                m_SurfaceAbove = enter;
                break;

            case eCollisionZone.Left:
                m_SurfaceLeft = enter;
                break;

            case eCollisionZone.Right:
                m_SurfaceRight = enter;
                break;
        }

        //Debug.LogFormat("Zone: {0}, Enter: {1}", zone, enter);
    }
}
