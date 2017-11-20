using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rewired;
using RewiredConsts;
using System;

public class InputManager : Singleton<InputManager>
{
    ///////////////////////////////////////////////////////////////////
    /// Constants
    ///////////////////////////////////////////////////////////////////
    public static readonly string Identifier = "InputManager";

    public static readonly int PrimaryPlayerId = 0;

    ///////////////////////////////////////////////////////////////////
    /// Serialized Field Member Variables
    ///////////////////////////////////////////////////////////////////

    ///////////////////////////////////////////////////////////////////
    /// Private Member Variables
    ///////////////////////////////////////////////////////////////////

    private Rewired.Player m_RewiredPlayer;                                     //! Reference of the main player.
    public Rewired.Player RewiredPlayer { get { return m_RewiredPlayer; } }

    private List<Action<InputActionEventData>> m_InputDelegateCache;    //! Use this to ensure we clear all delegates if the input manager is shut down before they are removed from the player.

    ///////////////////////////////////////////////////////////////////
    /// Singleton MonoBehaviour Implementation
    ///////////////////////////////////////////////////////////////////

    public override void Awake()
    {
        m_RewiredPlayer = ReInput.players.GetPlayer(InputManager.PrimaryPlayerId);
        m_InputDelegateCache = new List<Action<InputActionEventData>>();

		ReInput.ControllerConnectedEvent += OnControllerConnected;
		ReInput.ControllerDisconnectedEvent += OnControllerDisconnected;

		AssignPrimaryDevice();

        Debug.AssertFormat(ValidateManager() != false, "{0} : Failed to validate, please ensure that all required components are set and not null.", InputManager.Identifier);
        base.Awake();
    }

	public void AssignPrimaryDevice()
	{
		// TODO try to get this to work on PC.
		//m_RewiredPlayer.controllers.hasKeyboard = m_RewiredPlayer.controllers.joystickCount == 0;
	}

	private void OnControllerConnected(ControllerStatusChangedEventArgs args)
	{
		AssignPrimaryDevice();
	}

	private void OnControllerDisconnected(ControllerStatusChangedEventArgs args)
	{
		// TODO maybe a popup here!
		AssignPrimaryDevice();

		// would be dismissed by the user pressing a button on controller or keyboard
	}

    public override void OnDestroy()
    {
        // Handle the mouse input
        //m_RewiredPlayer.RemoveInputEventDelegate(OnMouseInput);

        // When we shut down, let's take care of all this junk.
        if (m_RewiredPlayer != null && m_InputDelegateCache != null && m_InputDelegateCache.Count > 0)
        {
            for (int i = 0; i < m_InputDelegateCache.Count; ++i)
            {
                m_RewiredPlayer.RemoveInputEventDelegate(m_InputDelegateCache[i]);
            }
            m_InputDelegateCache.Clear();
        }
        else if (m_InputDelegateCache != null && m_InputDelegateCache.Count > 0)
        {
            m_InputDelegateCache.Clear();
        }

		ReInput.ControllerConnectedEvent -= OnControllerConnected;
		ReInput.ControllerDisconnectedEvent -= OnControllerDisconnected;

        base.OnDestroy();
    }

    /// <summary>
    /// Validate that any serialized fields are properly set.
    /// A Valid Manager should function properly.
    /// </summary>
    /// <returns></returns>
    protected override bool ValidateManager()
    {
        bool isValid = true;

        isValid = isValid && (m_RewiredPlayer != null);
        isValid = isValid && base.ValidateManager();

        return isValid;
    }

    ///////////////////////////////////////////////////////////////////
    /// InputManager Implementation
    ///////////////////////////////////////////////////////////////////


    /// <summary>
    /// Add an input event delegate to the player. Use Rewired to handle this.. so great!
    /// </summary>
    /// <param name="inputDelegate"></param>
    /// <param name="updateType"></param>
    public void AddInputEventDelegate(Action<InputActionEventData> inputDelegate, UpdateLoopType updateType)
    {
        Debug.Assert(m_RewiredPlayer != null, "Rewired Player is null, cannot add input delegate!");

        if(m_RewiredPlayer != null)
        {
            m_RewiredPlayer.AddInputEventDelegate(inputDelegate, updateType);

            m_InputDelegateCache.Add(inputDelegate);
        }
    }

    /// <summary>
    /// Remove an input event delegate from the player. Use Rewired to handle this.. even better!
    /// </summary>
    /// <param name="inputDelegate"></param>
    public void RemoveInputEventDelegate(Action<InputActionEventData> inputDelegate)
    {
        Debug.Assert(m_RewiredPlayer != null && m_InputDelegateCache != null, "Rewired Player is null, or input cache is null cannot add input delegate!");

        if (m_RewiredPlayer != null && m_InputDelegateCache != null)
        {
            m_RewiredPlayer.RemoveInputEventDelegate(inputDelegate);

            bool didRemove = m_InputDelegateCache.Remove(inputDelegate);

            Debug.Assert(didRemove == true, "Attempted to remove delegate from the input cache but it was not found. This is odd, investigate.");
        }
    }
}