
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class float_controller : UdonSharpBehaviour
{
    public UdonBehaviour udonBehaviour;
    public string variable_string;
    public float custom_num = 0;
    [Tooltip("optional")]
    public Text display;

    private void Start()
    {
        //update display text to current float value if added
        object variable = udonBehaviour.GetProgramVariable(variable_string);
        if (variable != null)
        {
            if (variable.GetType() == typeof(float))
            {
                float num = (float)variable;
                updatevisual(num.ToString("0.00"));
            }
            else
            {
                Debug.LogWarning("float controller: variable not of float type");
            }
        }
        else
        {
            Debug.LogWarning("float controller: get variable returned null");
        }

    }

    public void variableadd() //add 1 to float
    {
        Debug.Log("add executed");
        calculatefloat(1);
    }

    public void variablesub() //subtract 1 from float
    {
        Debug.Log("sub executed");
        calculatefloat(-1);
    }

    public void variablepartadd() //add .1 to float
    {
        Debug.Log("add executed");
        calculatefloat(0.1f);
    }

    public void variablepartsub() //subtract .1 from float
    {
        Debug.Log("sub executed");
        calculatefloat(-0.1f);
    }

    public void variablecalcustom() //add a custom number to float
    {
        calculatefloat(custom_num);
    }

    public void variableset() //set float to custom number
    {
        udonBehaviour.SetProgramVariable(variable_string, custom_num);
        updatevisual(custom_num.ToString("0.00"));
    }

    public void updatevisual(string vistext) //update text display if added
    {
        if (display != null)
        {
            display.text = vistext;
        }
    }

    public void calculatefloat (float num) //calculates adding number to variable
    {
        object variable = (float)udonBehaviour.GetProgramVariable(variable_string);
        if (variable != null) //checks if variable is null
        {
            if (variable.GetType() == typeof(float)) //checks if variable is a floa type
            {
                //does the math and updates text display
                float result = ((float)variable + num);
                udonBehaviour.SetProgramVariable(variable_string, result);
                updatevisual(result.ToString("0.00"));
            }
            else
            {
                Debug.LogWarning("float controller: variable not of float type");
            }
        }
        else
        {
            Debug.LogWarning("float controller: get variable returned null");
        }
    }
}
