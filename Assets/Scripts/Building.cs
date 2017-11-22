using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Building : MonoBehaviour
{
    [SerializeField] private GameObject m_InteriorContainer;
    [SerializeField] private GameObject m_ExteriorContainer;
    [SerializeField] private SpriteRenderer m_DoorSprite;

    private void Awake()
    {
        VSEventManager.Instance.AddListener<GameEvents.BuildingChangeEvent>(OnPlayerBuildingChange);

        if (m_InteriorContainer != null)
        {
            // for now, this is how we decide if the building is enterable from the front
            ToggleBuildingVisibility(false);
        }
    }

    private void OnPlayerBuildingChange(GameEvents.BuildingChangeEvent e)
    {
        ToggleBuildingVisibility(e.Entered);
    }

    private void ToggleBuildingVisibility(bool entered)
    {
        m_ExteriorContainer.SetActive(!entered);

        if (m_InteriorContainer != null)
        {
            m_InteriorContainer.SetActive(entered);

            // toggle door alpha
            if (m_DoorSprite != null)
            {
                Color doorColor = m_DoorSprite.color;
                doorColor.a = (entered ? 0.25f : 1f);
                m_DoorSprite.color = doorColor;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            VSEventManager.Instance.TriggerEvent(new GameEvents.BuildingChangeEvent(true));
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            VSEventManager.Instance.TriggerEvent(new GameEvents.BuildingChangeEvent(false));
        }
    }
}
