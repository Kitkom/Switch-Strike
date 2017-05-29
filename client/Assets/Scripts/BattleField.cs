using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public enum BattleState {SWITCH, STRIKE1, READY, STRIKE2, RESULT};

public class BattleField : MonoBehaviour
{
	public NetworkEngine engine;
    public GameObject[] cards;  
	public Button[] buttons;
	public Button submit;
	public Text stateText;
	public LineRenderer[] lines;
    public float height;
    private bool[] lifted;
	public BattleState state;
	public int pressed;
	int switcha, switchb;

	int strikeFrom;
	public int[] strike;

    // Use this for initialization
    void Start()
    {
        lifted = new bool[8];
		strike = new int[4];
		state = BattleState.READY;
		EnableButton(1, false);
		EnableButton(0, true);
		strikeFrom = pressed = switcha = switchb = -1;
		submit.interactable = false;
		ResetStrike();
		ResetArrows();
		ShowArrows(false);
    }

    // Update is called once per frame
    void Update()
    {
		if (pressed != -1)
		{
			if (state == BattleState.READY)
			{
				switcha = pressed;
				Toggle(pressed);
				state = BattleState.SWITCH;
			}
			else if (state == BattleState.SWITCH)
			{
				if (pressed == switcha)
				{
					state = BattleState.READY;
				}
				else
				{
					switchb = pressed;
					Toggle(switcha);
					SwitchCard(switcha, switchb);
					//ResetArrows();
					//ShowArrows(true);
					state = BattleState.STRIKE1;
					stateText.text = "Set your targets.";
				}
			}
			else if (state == BattleState.STRIKE1)
			{
				if (pressed == 10)
				{
					Debug.Log("SUCC!");
					stateText.text = "Test succeeded.";
					submit.interactable= false;
				}
				else
				{
					strikeFrom = pressed;
					Toggle(pressed);
					EnableButton(0, false);
					EnableButton(1, true);
					state = BattleState.STRIKE2;
				}
			}
			else if (state == BattleState.STRIKE2)
			{
				if (pressed  != 10)
				{
					strike[strikeFrom] = pressed;
					RedirectArrow(strikeFrom, pressed);

					bool submitable = true;
					foreach (int i in strike)
					{
						submitable &= i >= 0;
					}

					EnableButton(1, false);
					EnableButton(0, true);

					Toggle(strikeFrom);
					submit.interactable = submitable;
					state = BattleState.STRIKE1;
				}
			}

			pressed = -1;
		}
    }

	public void SetPressed(int index)
	{
		pressed = index;
	}

    public void Toggle(int index)
    {
        if (lifted[index])
            cards[index].transform.position -= new Vector3(0, height, 0);
        else
            cards[index].transform.position += new Vector3(0, height, 0);
        lifted[index] = !lifted[index];
    }

	void ResetArrows()
	{
		for (int i = 0; i < 8; ++i)
		{
			RedirectArrow(i, i);
		}
	}

	void ShowArrows(bool enable)
	{
		foreach (LineRenderer line in lines)
		{
			line.enabled = enable;
		}
	}

	void RedirectArrow(int a, int b)
	{
		LineRenderer line = lines[a];
		line.enabled = true;
		line.SetPosition(1, cards[b].transform.position + new Vector3(0, 0.3, 0));
	}

	void ResetStrike()
	{
		for (int i = 0; i < 4; ++i)
		{
			strike[i] = -1;
		}
	}

	void SwitchCard(int a, int b)
	{
		Vector3 temp = cards[a].transform.position;
		cards[a].transform.position = cards[b].transform.position;
		cards[b].transform.position = temp;

		GameObject tempo = cards[a];
		cards[a] = cards[b];
		cards[b] = tempo;
	}

	void EnableButton(int k, bool enabled)
	{
		for (int i = 0; i < 4; ++i)
		{
			
			buttons[i + 4 * k].interactable = enabled;
		}
	}

}
