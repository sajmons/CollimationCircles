#pragma once

#include "ASICamera2.h"

// CCtrlPanel

class CCtrlPanel : public CStatic
{
	DECLARE_DYNAMIC(CCtrlPanel)

public:
	CCtrlPanel();
	virtual ~CCtrlPanel();

protected:
	DECLARE_MESSAGE_MAP()
	CStatic *p_MyStatic;
	CSliderCtrl *p_MySlider;
	CEdit *p_MyEdit;
	CButton *p_MyCheck; 
// 	CComboBox *p_MyCombo; 
	CScrollBar m_scrollV_ctrl;
	CRect rc_slider, rc_client;
public:

	bool bShowCtrl, bCtrlDrawDone;
	int iNumOfCtrl;
	int* p_CtrlOriyStart;
	int iCamIndex;
	ASI_CONTROL_CAPS** pControlCaps;//store each ASI_CONTROL_CAPS's pointer
	bool *pRefreshedIndex;
	CToolTipCtrl m_ToolTip;	
	int iCtrlYEnd, iCtrlYStart;
	float fSensorTempe;

	void RefreshSliderEdit(int iCtrlIndex, int CtrlType, long lVlaue);
	void EnableControl(int iControlIndex, bool bAuto, bool bWriteable);
	void GetCtrlsData(int iCtrlNum, ASI_CONTROL_CAPS* Caps, bool* bRefresh, bool bShow, int Camindex);
	void DrawCtrl();
	virtual BOOL PreTranslateMessage(MSG* pMsg);
	void ControlSetValue(int iControlIndex);
	void Value2Edit(long lValue, int iControlIndex, ASI_CONTROL_TYPE iControlType);
	void EditSetValue();
	void SetAuto(int iControlIndex, bool bChecked);
	void MoveCtrl(int deltaY);
	void DeleteAllCtrl();
	void RefreshValue(int iControlIndex, int iControlIndex0);
	void RefreshAllValue(int iControlIndex0);
	float getSensorTemp(){return fSensorTempe;};

	afx_msg void OnShowWindow(BOOL bShow, UINT nStatus);
	afx_msg void OnVScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar);
	afx_msg void OnTimer(UINT_PTR nIDEvent);
	afx_msg int OnCreate(LPCREATESTRUCT lpCreateStruct);

};


