using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class consoler : MonoBehaviour {

    private string text;

	// Use this for initialization
	void Start () {
        clear();
	}

    public void clear()
    {
        text = "";
        commit();
    }

    void commit()
    {
        GetComponent<TextMesh>().text = text;
    }

    public void add(string line)
    {
        //text = line +"\n" + text;
        //commit();
        //Debug.Log(line);
    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
