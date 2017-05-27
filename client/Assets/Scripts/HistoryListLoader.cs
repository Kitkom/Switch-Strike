using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class HistoryListLoader : MonoBehaviour
{
	public Dropdown historyList;
	private List<Dropdown.OptionData> list;
	// Use this for initialization
	void Start()
	{
		list = historyList.options;
		list.Clear ();
		list.Add (new Dropdown.OptionData ("1.Alpha"));
		list.Add (new Dropdown.OptionData ("2.Beta"));
		list.Add (new Dropdown.OptionData ("3.Gamma"));
		list.Add (new Dropdown.OptionData ("4.Alpha"));
		list.Add (new Dropdown.OptionData ("5.Beta"));
		list.Add (new Dropdown.OptionData ("6.Gamma"));
		list.Add (new Dropdown.OptionData ("7.Alpha"));
		list.Add (new Dropdown.OptionData ("8.Beta"));
		list.Add (new Dropdown.OptionData ("9.Gamma"));

	}

	// Update is called once per frame
	void Update()
	{

	}
}
