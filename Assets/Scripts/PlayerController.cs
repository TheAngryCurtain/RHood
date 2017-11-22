using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform m_CachedTransform;
    [SerializeField] private Rigidbody2D m_Rigidbody;
    [SerializeField] private Transform m_ModelContainer;
    [SerializeField] private Transform m_AimTargetTransform;
    [SerializeField] private GameObject m_ArrowPrefab;
    [SerializeField] private Sack m_Sack;
    //[SerializeField] private DistanceJoint2D m_GrappleJoint;

    [SerializeField] private float m_DefaultMoveSpeed = 5f;
    [SerializeField] private float m_CrouchedMoveSpeed = 3f;
    [SerializeField] private float m_MaxJumpVelocity = 6f;
    [SerializeField] private float m_MinJumpVelocity = 2f;
    [SerializeField] private float m_AccelerationGrounded = 0.5f;
    [SerializeField] private float m_AccelerationAirborne = 0.75f;
    [SerializeField] private float m_WallSlideVelocity = -1f;
    [SerializeField] private float m_ShootForce = 5f;
    [SerializeField] private float m_WallJumpXForce = 5f;
    [SerializeField] private float m_ClimbSpeed = 2f;

    private bool m_TouchingJumpableSurface { get { return m_SurfaceBelow || m_SurfaceLeft || m_SurfaceRight; } }

    private float m_MovementX;
    private float m_MovementY;
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
    private Arrow m_PrevGrappleArrow;

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
                m_MovementX = 0f;

                float h = data.GetAxis();
                if (h != 0f)
                {
                    m_MovementX = h * (m_Crouched ? m_CrouchedMoveSpeed : m_DefaultMoveSpeed);
                }
                break;

            // climb
            case RewiredConsts.Action.Move_Vertical:
                m_MovementY = 0f;

                float v = data.GetAxis();
                if (v != 0f)
                {
                    m_MovementY = v;
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
            Climb();
        }
        else if (!m_SurfaceBelow)
        {
            // you're not grappling and in the air, attempt to right the player
            m_Rigidbody.MoveRotation(Mathf.Lerp(m_Rigidbody.rotation, 0f, 0.2f));
        }
    }

    private void Move()
    {
        if (m_Rigidbody != null)
        {
            Vector2 velocity = m_Rigidbody.velocity;
            float direction = Mathf.Sign(m_MovementX);

            if (!m_Grappling)
            {
                // block moving into a wall
                if ((direction < 0f && !m_SurfaceLeft) || (direction > 0f && !m_SurfaceRight))
                {
                    float smoothTime = m_AccelerationAirborne;
                    //float modifiedMovement = m_MovementX;
                    if (m_TouchingJumpableSurface)
                    {
                        smoothTime = m_AccelerationGrounded;
                        //modifiedMovement = m_MovementX * (1f - m_Sack.WeightPercent);
                    }

                    //velocity.x = Mathf.SmoothDamp(velocity.x, modifiedMovement, ref m_VelXSmooth, smoothTime);
                    velocity.x = Mathf.SmoothDamp(velocity.x, m_MovementX, ref m_VelXSmooth, smoothTime);

                }
                else
                {
                    // sliding down wall
                    velocity.y = m_WallSlideVelocity;
                }
            }
            else
            {
                // add momentum during swinging?
                m_Rigidbody.AddForce(Vector2.right * m_MovementX * 0.5f * (1f - m_Sack.WeightPercent), ForceMode2D.Force);
            }

            FlipSprite(direction);
            m_Rigidbody.velocity = velocity;
        }
    }

    private void Climb()
    {
        m_PrevGrappleArrow.Reel(m_MovementY * m_ClimbSpeed * (1f - m_Sack.WeightPercent));
    }

    private void FlipSprite(float direction)
    {
        Vector3 localScale = m_ModelContainer.localScale;
        localScale.x = (direction < 0f ? -1 : 1);
        m_ModelContainer.localScale = localScale;
    }

    private void Jump()
    {
        if ((m_TouchingJumpableSurface || m_Grappling) && m_RequestJump)
        {
            if (m_Grappling)
            {
                m_PrevGrappleArrow.BreakGrapple();
                m_Grappling = false;
            }

            Vector2 velocity = m_Rigidbody.velocity;

            // wall jump
            if (m_SurfaceLeft || m_SurfaceRight)
            {
                velocity.x += m_WallJumpXForce * -Mathf.Sign(m_MovementX);
            }

            // account for sack weight
            float deltaVelocity = m_MaxJumpVelocity - m_MinJumpVelocity;
            float weightedVelocity = m_MaxJumpVelocity - (m_Sack.WeightPercent * deltaVelocity);
            velocity.y = weightedVelocity;

            Debug.LogFormat("weight %: {0}, weighted vel: {1}, min vel: {2}, max vel: {3}", m_Sack.WeightPercent, weightedVelocity, m_MinJumpVelocity, m_MaxJumpVelocity);

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
                m_AimTargetTransform.position = m_CachedTransform.position + m_TargetPos;
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
            System.Action callback = null;
            if (grapple)
            {
                if (m_Grappling)
                {
                    m_PrevGrappleArrow.BreakGrapple();
                }

                if (m_PrevGrappleArrow != null)
                {
                    m_PrevGrappleArrow.CancelGrapple();
                }

                callback = () => { m_Grappling = true; };
                m_PrevGrappleArrow = arrow;
            }

            Vector3 direction = (m_AimTargetTransform.position - m_CachedTransform.position).normalized;
            arrow.Launch(direction, m_ShootForce, m_CachedTransform, callback);

            // push player back
            m_Rigidbody.AddForce(-direction * 2f, ForceMode2D.Impulse);
        }
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

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (collider.gameObject.layer == LayerMask.NameToLayer("Treasure"))
        {
            Treasure t = collider.gameObject.GetComponent<Treasure>();
            if (t != null)
            {
                int value = t.Value;
                bool accepted = m_Sack.Fill(value);
                if (accepted)
                {
                    Destroy(collider.gameObject);
                }
                else
                {
                    t.Reject();
                }
            }
            else
            {
                Debug.LogError("No treasure component found");
            }
        }
    }
}
