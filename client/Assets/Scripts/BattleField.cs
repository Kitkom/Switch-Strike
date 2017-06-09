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
	public Text stateText, selfHpText, oppoHpText;
	public LineRenderer[] lines;
    public float height;
    private bool[] lifted;
	public BattleState state;
	public byte pressed;
	public byte switcha, switchb;

    public GameObject resultBlocker;

	byte strikeFrom;
	public byte[] strike;

    public bool enableResult;

    // Use this for initialization
    void Start()
    {
        lifted = new bool[8];
		strike = new byte[4];
		state = BattleState.READY;
		EnableButton(1, false);
		EnableButton(0, true);
		strikeFrom = pressed = switcha = switchb = 255;
		submit.interactable = false;
		ResetStrike();
		ResetArrows();
		ShowArrows(false);
    }

    // Update is called once per frame
    void Update()
    {
		if (pressed != 255)
		{
			if (state == BattleState.READY)
			{
                ResetStrike();
				switcha = pressed;
				Toggle(pressed);
				state = BattleState.SWITCH;
			}
			else if (state == BattleState.SWITCH)
			{
				Toggle(switcha);
				if (pressed == switcha)
				{
					state = BattleState.READY;
				}
				else
				{
					switchb = pressed;
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
					stateText.text = "";
                    engine.Action();
                    state = BattleState.READY;
                    submit.gameObject.SetActive(false);
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
                    if (Mathf.Abs((int)(strikeFrom) - (int)(pressed - 4)) < 2)
                    {

                        strike[strikeFrom] = pressed;
                        RedirectArrow(strikeFrom, pressed);

                        bool submitable = true;
                        foreach (int i in strike)
                        {
                            submitable &= i < 255;
                        }

                        EnableButton(1, false);
                        EnableButton(0, true);

                        Toggle(strikeFrom);
                        submit.gameObject.SetActive(submitable);
                        submit.interactable = submitable;
                        state = BattleState.STRIKE1;
                    }
				}
			}

			pressed = 255;
		}
        if (enableResult)
        {
            enableResult = false;
            resultBlocker.SetActive(true);
            SwitchCard(engine.oswitcha + 4, engine.oswitchb + 4);
            Toggle(engine.oswitcha + 4);
            Toggle(engine.oswitchb + 4);
            for (int i = 0; i < 4; ++i)
                RedirectArrow(4 + i, engine.ostrike[i]);
            ShowArrows(true);
        }
        if (engine.refreshHp)
        {
            engine.refreshHp = false;
            selfHpText.text = engine.selfHp.ToString();
            oppoHpText.text = engine.oppoHp.ToString();

        }
    }

	public void SetPressed(int index)
	{
		pressed = (byte)index;
	}

    public void ResetResult()
    {
        Toggle(engine.oswitcha + 4);
        Toggle(engine.oswitchb + 4);
        selfHpText.text = engine.selfHp.ToString();
        oppoHpText.text = engine.oppoHp.ToString();
        ShowArrows(false);
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
		line.SetPosition(1, cards[b].transform.position + new Vector3(0, (float)0.3f, 0));
	}

	void ResetStrike()
	{
		for (int i = 0; i < 4; ++i)
		{
			strike[i] = 255;
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
