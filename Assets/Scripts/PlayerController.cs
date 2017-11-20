using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private SpriteRenderer m_Renderer;
    [SerializeField] private Transform m_AimTargetTransform;
    [SerializeField] private GameObject m_ArrowPrefab;
    [SerializeField] private DistanceJoint2D m_GrappleJoint;
    [SerializeField] private LineRenderer m_GrappleRenderer;

    [SerializeField] private float m_DefaultMoveSpeed = 5f;
    [SerializeField] private float m_CrouchedMoveSpeed = 3f;
    [SerializeField] private float m_MaxJumpVelocity = 6f;
    [SerializeField] private float m_MinJumpVelocity = 2f;
    [SerializeField] private float m_AccelerationGrounded = 0.5f;
    [SerializeField] private float m_AccelerationAirborne = 0.75f;
    [SerializeField] private float m_WallSlideVelocity = -1f;
    [SerializeField] private float m_ShootForce = 5f;

    private bool m_TouchingJumpableSurface { get { return m_SurfaceBelow || m_SurfaceLeft || m_SurfaceRight; } }

    private float m_Movement;
    private float m_VelXSmooth;
    private bool m_RequestJump = false;
    private bool m_CancelJump = false;
    private bool m_Crouched = false;

    private bool m_SurfaceBelow = false;
    private bool m_SurfaceLeft = false;
    private bool m_SurfaceRight = false;
    private bool m_SurfaceAbove = false;

    private bool m_Aiming = false;
    private bool m_Grappling = false;
    private Vector3 m_TargetPos = Vector3.zero;

    private void Awake()
    {
        InputManager.Instance.AddInputEventDelegate(OnInputRecieved, UpdateLoopType.Update);
    }

    private void OnInputRecieved(InputActionEventData data)
    {
        switch (data.actionId)
        {
            // move
            case RewiredConsts.Action.Move_Horizontal:
                m_Movement = 0f;

                float h = data.GetAxis();
                if (h != 0f)
                {
                    m_Movement = h * (m_Crouched ? m_CrouchedMoveSpeed : m_DefaultMoveSpeed);
                }
                break;

            // shoot/grapple
            case RewiredConsts.Action.Shoot: // fall through
            case RewiredConsts.Action.Grapple:
                if (data.GetButton())
                {
                    m_Aiming = true;
                }
                else if (data.GetButtonUp())
                {
                    ShowAimTarget(false);
                    m_Aiming = false;

                    Shoot(data.actionId == RewiredConsts.Action.Grapple);
                }
                break;

            // aim
            case RewiredConsts.Action.Aim_Horizontal:
                m_TargetPos.x = 0f;

                float aH = data.GetAxis();
                if (aH != 0f)
                {
                    m_TargetPos.x = aH;
                }
                break;

            case RewiredConsts.Action.Aim_Vertical:
                m_TargetPos.y = 0f;

                float aV = data.GetAxis();
                if (aV != 0f)
                {
                    m_TargetPos.y = aV;
                }
                break;

            // jump
            case RewiredConsts.Action.Jump:
                if (data.GetButtonDown() && (m_TouchingJumpableSurface || m_Grappling))
                {
                    m_RequestJump = true;
                }
                else if (data.GetButtonUp())
                {
                    m_CancelJump = true;
                }
                break;

            // crouch
            case RewiredConsts.Action.Crouch:
                if (data.GetButtonDown() && m_SurfaceBelow)
                {
                    m_Crouched = !m_Crouched;
                    Debug.LogFormat("Crouched: {0}", m_Crouched);
                }
                break;
        }
    }

    private void FixedUpdate()
    {
        Move();
        Jump();
        CancelJump();
        Aim();

        if (m_Grappling)
        {
            m_GrappleRenderer.SetPosition(1, m_CachedTransform.position);
        }
    }

    private void Move()
    {
        if (m_Rigidbody != null && !m_Grappling)
        {
            Vector2 velocity = m_Rigidbody.velocity;
            
            // block moving into a wall
            float direction = Mathf.Sign(m_Movement);
            if ((direction < 0f && !m_SurfaceLeft) || (direction > 0f && !m_SurfaceRight))
            {
                float smoothTime = (m_TouchingJumpableSurface ? m_AccelerationGrounded : m_AccelerationAirborne);
                velocity.x = Mathf.SmoothDamp(velocity.x, m_Movement, ref m_VelXSmooth, smoothTime);
            }
            else
            {
                // sliding down wall
                velocity.y = m_WallSlideVelocity;
            }

            // flip sprite
            m_Renderer.flipX = direction < 0f;

            m_Rigidbody.velocity = velocity;
        }
    }

    private void Jump()
    {
        if ((m_TouchingJumpableSurface || m_Grappling) && m_RequestJump)
        {
            if (m_Grappling)
            {
                CancelGrapple();
            }

            Vector2 velocity = m_Rigidbody.velocity;
            velocity.y = m_MaxJumpVelocity;

            m_Rigidbody.velocity = velocity;
            m_RequestJump = false;
            m_Crouched = false;
        }
    }

    private void CancelJump()
    {
        if (m_CancelJump)
        {
            Vector2 velocity = m_Rigidbody.velocity;
            if (velocity.y > m_MinJumpVelocity)
            {
                velocity.y = m_MinJumpVelocity;

                m_Rigidbody.velocity = velocity;
            }
            m_CancelJump = false;
        }
    }

    private void Aim()
    {
        if (m_Aiming)
        {
            if (m_TargetPos != Vector3.zero)
            {
                ShowAimTarget(true);
                m_AimTargetTransform.localPosition = m_TargetPos;
            }
            else
            {
                ShowAimTarget(false);
            }
        }
    }

    private void Shoot(bool grapple)
    {
        GameObject arrowObj = (GameObject)Instantiate(m_ArrowPrefab, null);
        arrowObj.transform.position = m_AimTargetTransform.position;

        Arrow arrow = arrowObj.GetComponent<Arrow>();
        if (arrow != null)
        {
            System.Action<Rigidbody2D> callback = null;
            if (grapple)
            {
                callback = OnGrappleArrowLanded;
            }

            Vector3 direction = (m_AimTargetTransform.position - m_CachedTransform.position).normalized;
            arrow.Launch(direction, m_ShootForce, callback);
        }
    }

    private void OnGrappleArrowLanded(Rigidbody2D arrowRb)
    {
        if (m_Grappling)
        {
            CancelGrapple();
        }

        Vector3 arrowPos = arrowRb.transform.position;
        m_GrappleJoint.distance = (arrowPos - m_CachedTransform.position).magnitude;
        m_GrappleJoint.connectedBody = arrowRb;
        m_GrappleJoint.enabled = true;

        // set static end of line
        m_GrappleRenderer.enabled = true;
        m_GrappleRenderer.positionCount = 2;
        m_GrappleRenderer.SetPosition(0, arrowPos);
        m_GrappleRenderer.SetPosition(1, m_CachedTransform.position);

        m_Grappling = true;
    }

    private void CancelGrapple()
    {
        m_GrappleJoint.connectedBody = null;
        m_GrappleJoint.enabled = false;

        m_Grappling = false;
        m_GrappleRenderer.enabled = false;
    }

    private void ShowAimTarget(bool show)
    {
        m_AimTargetTransform.gameObject.SetActive(show);
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
