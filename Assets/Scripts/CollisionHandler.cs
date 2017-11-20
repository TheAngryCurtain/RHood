using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum eCollisionZone { Below, Above, Left, Right }

public class CollisionHandler : MonoBehaviour
{
    [SerializeField] private PlayerController m_Controller;
    [SerializeField] private eCollisionZone m_Zone;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Terrain"))
        {
            m_Controller.HandleTriggerCollision(m_Zone, true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Terrain"))
        {
            m_Controller.HandleTriggerCollision(m_Zone, false);
        }
    }
}
