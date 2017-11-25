using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;

public class PlayerController : Controller
{
    //[SerializeField] private Transform m_AimTargetTransform;
    [SerializeField] private Sack m_Sack;
    [SerializeField] private Transform m_SackModelContainer;
    [SerializeField] private GameObject m_GrappleArrowPrefab;

    [SerializeField] private float m_CrouchedMoveSpeed = 3f;
    [SerializeField] private float m_WallSlideVelocity = -1f;
    [SerializeField] private float m_WallJumpXForce = 5f;
    [SerializeField] private float m_ClimbSpeed = 2f;

    private bool m_CarryingSack { get { return m_Sack.transform.parent == m_SackModelContainer; } }
    private float m_SackWeightModifier { get { return m_CarryingSack ? 1f - m_Sack.WeightPercent : 1f; } }
    private float m_SackWeightPercent { get { return m_CarryingSack ? m_Sack.WeightPercent : 0f; } }

    private float m_MovementY;
    private bool m_CancelJump = false;
    private bool m_Crouched = false;

    private bool m_Grappling = false;
    private Vector3 m_TargetPos = Vector3.zero;
    private Arrow m_PrevGrappleArrow;

    private bool m_CanPickupSack = false;
    private bool m_InBuilding = false;
    private bool m_InDoorway = false;

    private void Awake()
    {
        InputManager.Instance.AddInputEventDelegate(OnInputRecieved, UpdateLoopType.Update);

        VSEventManager.Instance.AddListener<GameEvents.BuildingChangeEvent>(OnBuildingChanged);

        Physics2D.IgnoreCollision(m_Sack.GetComponent<Collider2D>(), GetComponent<Collider2D>());
        m_Sack.Handle(m_CarryingSack, 1f);
    }

    private void OnBuildingChanged(GameEvents.BuildingChangeEvent e)
    {
        m_InBuilding = e.Entered;
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

            // interact
            case RewiredConsts.Action.Interact:
                if (data.GetButtonDown())
                {
                    // entering doors
                    if (m_InDoorway && !m_Grappling)
                    {
                        VSEventManager.Instance.TriggerEvent(new GameEvents.BuildingChangeEvent(!m_InBuilding));
                    }
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

                    PrepareShoot(data.actionId == RewiredConsts.Action.Grapple);
                }
                break;

            // drop/pickup sack
            case RewiredConsts.Action.Drop_Sack:
                if (data.GetButtonDown())
                {
                    GameObject sackObj = m_Sack.gameObject;
                    float direction = Mathf.Sign(m_MovementX);
                    if (m_CarryingSack)
                    {
                        sackObj.transform.SetParent(null);
                        m_Sack.Handle(false, direction);
                    }
                    else
                    {
                        if (m_CanPickupSack)
                        {
                            sackObj.transform.SetParent(m_SackModelContainer);
                            sackObj.transform.localPosition = m_Sack.ModelOffset;
                            sackObj.transform.localRotation = Quaternion.identity;

                            m_Sack.Handle(true, direction);
                        }
                    }
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

#if UNITY_EDITOR
    private void OnGUI()
    {
        GUI.Label(new Rect(10, 0, 300, 30), "Velocity: " + m_Rigidbody.velocity);
        GUI.Label(new Rect(10, 10, 300, 30), "Surface Below: " + m_SurfaceBelow);
        GUI.Label(new Rect(10, 20, 300, 30), "Surface Above: " + m_SurfaceAbove);
        GUI.Label(new Rect(10, 30, 300, 30), "Surface Left: " + m_SurfaceLeft);
        GUI.Label(new Rect(10, 40, 300, 30), "Surface Right: " + m_SurfaceRight);
    }
#endif

    private void FixedUpdate()
    {
        PrepareMove();
        PrepareJump();
        CancelJump();
        Aim(m_TargetPos);

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

    private Vector2 GetMoveVelocity()
    {
        Vector2 velocity = m_Rigidbody.velocity;
        float direction = Mathf.Sign(m_MovementX);

        // block moving into a wall
        if ((direction < 0f && !m_SurfaceLeft) || (direction > 0f && !m_SurfaceRight))
        {
            float smoothTime = m_AccelerationAirborne;
            //float modifiedMovement = m_MovementX;
            if (m_TouchingJumpableSurface)
            {
                smoothTime = m_AccelerationGrounded;
                //modifiedMovement = m_MovementX * m_SackWeightModifier;
            }

            //velocity.x = Mathf.SmoothDamp(velocity.x, modifiedMovement, ref m_VelXSmooth, smoothTime);
            velocity.x = Mathf.SmoothDamp(velocity.x, m_MovementX, ref m_VelXSmooth, smoothTime);

        }
        else
        {
            // sliding down wall
            velocity.y = m_WallSlideVelocity;
        }

        return velocity;
    }

    private void PrepareMove()
    {
        Vector2 velocity = m_Rigidbody.velocity;
        //float direction = Mathf.Sign(m_MovementX);

        if (!m_Grappling)
        {
            velocity = GetMoveVelocity();
        }
        else
        {
            // add momentum during swinging?
            m_Rigidbody.AddForce(Vector2.right * m_MovementX * 0.5f * m_SackWeightModifier, ForceMode2D.Force);
        }

        Move(velocity);
    }

    private void Climb()
    {
        m_PrevGrappleArrow.Reel(m_MovementY * m_ClimbSpeed * m_SackWeightModifier);
    }

    private Vector2 GetJumpVelocity()
    {
        Vector2 velocity = m_Rigidbody.velocity;
        // wall jump
        if (m_SurfaceLeft || m_SurfaceRight)
        {
            velocity.x += m_WallJumpXForce * -Mathf.Sign(m_MovementX);
        }

        // account for sack weight
        float deltaVelocity = m_MaxJumpVelocity - m_MinJumpVelocity;
        float weightedVelocity = m_MaxJumpVelocity - (m_SackWeightPercent * deltaVelocity);
        velocity.y = weightedVelocity;

        Debug.LogFormat("weight %: {0}, weighted vel: {1}, min vel: {2}, max vel: {3}", m_SackWeightPercent, weightedVelocity, m_MinJumpVelocity, m_MaxJumpVelocity);

        return velocity;
    }

    private void PrepareJump()
    {
        m_CanJump = (m_TouchingJumpableSurface || m_Grappling);
        if (m_CanJump && m_RequestJump)
        {
            if (m_Grappling)
            {
                m_PrevGrappleArrow.BreakGrapple();
                m_Grappling = false;
            }

            Vector2 velocity = GetJumpVelocity();
            Jump(velocity);
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

    //private void PrepareAim()
    //{
        //if (m_Aiming)
        //{
        //    if (m_TargetPos != Vector3.zero)
        //    {
        //        ShowAimTarget(true);
        //        m_AimTargetTransform.position = m_CachedTransform.position + m_TargetPos;
        //    }
        //    else
        //    {
        //        ShowAimTarget(false);
        //    }

        //    Aim(m_TargetPos - m_CachedTransform.position);
        //}
    //}

    private void PrepareShoot(bool grapple)
    {
        m_ActiveArrowObj = m_ArrowPrefab;

        System.Action callback = null;
        if (grapple)
        {
            m_ActiveArrowObj = m_GrappleArrowPrefab;

            if (m_Grappling)
            {
                m_PrevGrappleArrow.BreakGrapple();
                m_PrevGrappleArrow = null;
            }

            if (m_PrevGrappleArrow != null)
            {
                m_PrevGrappleArrow.CancelGrapple();
            }

            callback = () => { m_Grappling = true; };
        }

        //Vector3 direction = (m_AimTargetTransform.position - m_CachedTransform.position).normalized;
        Arrow arrow = Shoot(m_AimDirection, callback);
        m_PrevGrappleArrow = arrow;
    }

    private void ShowAimTarget(bool show)
    {
        //m_AimTargetTransform.gameObject.SetActive(show);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (m_CarryingSack && collider.gameObject.layer == LayerMask.NameToLayer("Treasure"))
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
        else if (collider.gameObject.layer == LayerMask.NameToLayer("Sack"))
        {
            m_CanPickupSack = true;
        }
        else if (collider.gameObject.layer == LayerMask.NameToLayer("Door"))
        {
            m_InDoorway = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collider)
    {
        if (collider.gameObject.layer == LayerMask.NameToLayer("Sack"))
        {
            m_CanPickupSack = false;
        }
        else if (collider.gameObject.layer == LayerMask.NameToLayer("Door"))
        {
            m_InDoorway = false;
        }
    }
}
