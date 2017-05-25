 using UnityEngine;
using System.Collections;

public class BattleField : MonoBehaviour
{
    public GameObject[] cards;  
    public float height;

    private bool[] lifted;
    // Use this for initialization
    void Start()
    {
        lifted = new bool[8];
    }
	
    // Update is called once per frame
    void Update()
    {
	
    }

    public void Toggle(int index)
    {
        Debug.Log(index);
        if (lifted[index])
            cards[index].transform.position -= new Vector3(0, height, 0);
        else
            cards[index].transform.position += new Vector3(0, height, 0);
        lifted[index] = !lifted[index];
    }

}
