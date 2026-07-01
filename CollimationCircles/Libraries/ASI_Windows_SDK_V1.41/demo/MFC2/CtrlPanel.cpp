// CtrlPanel.cpp : 
//

#include "stdafx.h"
#include "CtrlPanel.h"


// CCtrlPanel

IMPLEMENT_DYNAMIC(CCtrlPanel, CStatic)

CCtrlPanel::CCtrlPanel()
{
	bShowCtrl = false;
	p_CtrlOriyStart = 0;
	p_MyStatic = 0;
	p_MySlider = 0;
	p_MyEdit = 0;
	p_MyCheck = 0; 
//	p_MyCombo = 0;
	bCtrlDrawDone = false;
	m_ToolTip.Create(this);
	iCtrlYStart = iCtrlYEnd = 0;
	pControlCaps = NULL;
	pRefreshedIndex = NULL;
}

CCtrlPanel::~CCtrlPanel()
{
	if(pControlCaps)
	{
		delete[] pControlCaps;//pointer is deleted, but the data pointed is not release
		pControlCaps = 0;
	}
	DeleteAllCtrl();
}


BEGIN_MESSAGE_MAP(CCtrlPanel, CStatic)
	ON_WM_PAINT()
	ON_WM_SHOWWINDOW()
	ON_WM_VSCROLL()
	ON_WM_TIMER()
	ON_WM_CREATE()
END_MESSAGE_MAP()



// CCtrlPanel 

void CCtrlPanel::DeleteAllCtrl()
{
	if(p_MyStatic)
	{
		delete[] p_MyStatic;
		p_MyStatic = 0;
	}
	if(p_MySlider)
	{
		delete[] p_MySlider;
		p_MySlider = 0;
	}
	if(p_MyEdit)
	{
		delete[] p_MyEdit;
		p_MyEdit = 0;
	}
	if(p_MyCheck)
	{
		delete[] p_MyCheck; 
		p_MyCheck = 0;
	}
	/*if(p_MyCombo)
	{
		delete p_MyCombo;
		p_MyCombo = 0;
	}*/
	if(p_CtrlOriyStart)
	{
		delete[] p_CtrlOriyStart;
		p_CtrlOriyStart = 0;
	}

}


void CCtrlPanel::GetCtrlsData(int iCtrlNum, ASI_CONTROL_CAPS* Caps, bool* bRefresh, bool bShow, int Camindex)
{
	if(bShow)
	{
		int i;
		iNumOfCtrl = iCtrlNum;
		if(pControlCaps)
		{
			delete[] pControlCaps;//pointer is deleted, but the data pointed is not release
			pControlCaps = 0;
		}
		pControlCaps = new ASI_CONTROL_CAPS*[iNumOfCtrl];
		for(i = 0; i< iNumOfCtrl; i++) 
			pControlCaps[i] = &Caps[i];
	
		pRefreshedIndex = bRefresh;
		iCamIndex = Camindex;
	}
	bShowCtrl = bShow;
	DrawCtrl();

}
void CCtrlPanel::DrawCtrl()
{
	int i; 
	long lControlValue;
	ASI_BOOL bIsAuto = ASI_FALSE;
	CString cs;

	CRect rc;
	int ySpace = 30, yStart, iCountID = 2000;
	DWORD WD_Style;
	bCtrlDrawDone = false;

	if(bShowCtrl)
	{

		DeleteAllCtrl();
		p_MyStatic = new CStatic[iNumOfCtrl];
		p_MySlider = new CSliderCtrl[iNumOfCtrl];
		p_MyEdit = new CEdit[iNumOfCtrl];
		p_MyCheck = new CButton[iNumOfCtrl];
		p_CtrlOriyStart = new int[iNumOfCtrl];
		ASI_CONTROL_CAPS* pCap;

		for(i = 0; i < iNumOfCtrl; i++)
		{	
			pCap = pControlCaps[i];

			//static
			rc.left = rc_client.left;
			rc.right = rc.left + 140;
			rc.top = rc_client.top;
			rc.bottom = rc.top + 25;
			yStart = rc.top + 20;
			p_CtrlOriyStart[i] = yStart + i*ySpace;
			rc.MoveToY(p_CtrlOriyStart[i]);
			WD_Style	= 0x50020102;
			p_MyStatic[i].Create(pCap->Name, WD_Style , rc, this, iCountID++ );
			if(pCap->ControlType == ASI_EXPOSURE)
				m_ToolTip.AddTool((CWnd*)&p_MyStatic[i],"unit ms");
			else
				m_ToolTip.AddTool((CWnd*)&p_MyStatic[i],pCap->Description);

	

			//slider
			rc.left = rc.right;
			rc.right = rc.left + 190;
			rc.top = rc_client.top;
			rc.bottom = rc.top + 25;
			rc.MoveToY(p_CtrlOriyStart[i]);
			WD_Style	= 0x50010118;
			p_MySlider[i].Create(WD_Style, rc, this, iCountID++ );
			if(pCap->ControlType == ASI_EXPOSURE)
				p_MySlider[i].SetRange(0, 10000, TRUE);
			else if(pCap->ControlType == ASI_TEMPERATURE)
				p_MySlider[i].SetRange(pCap->MinValue/10, pCap->MaxValue/10, TRUE);
			else
				p_MySlider[i].SetRange(pCap->MinValue, pCap->MaxValue, TRUE);

			//edit
			WD_Style	= 0x50811080;
			rc.left = rc.right;
			rc.right = rc.left + 54;
			rc.top = rc_client.top;
			rc.bottom = rc.top + 25;

			rc.MoveToY(p_CtrlOriyStart[i]);

			p_MyEdit[i].Create(WD_Style , rc, this, iCountID++ );
			ASIGetControlValue(iCamIndex, pCap->ControlType, &lControlValue, &bIsAuto);
			if(pCap->ControlType == ASI_EXPOSURE)
				lControlValue/=1000;
			RefreshSliderEdit(i, pCap->ControlType, lControlValue);
			if(bIsAuto||pCap->IsWritable == ASI_FALSE)
				pRefreshedIndex[i] = true;
			else
				pRefreshedIndex[i] = false;

			EnableControl(i, bIsAuto,pCap->IsWritable == ASI_TRUE?true:false);
			

			//check
			rc.left = rc.right + 5;
			rc.right = rc.left + 55;
			rc.top = rc_client.top;
			rc.bottom = rc.top + 25;

			rc.MoveToY(p_CtrlOriyStart[i]);
			WD_Style	= 0x50010003;

			p_MyCheck[i].Create("Auto", WD_Style , rc, this, iCountID++ );		
			
			p_MyCheck[i].ShowWindow(pCap->IsAutoSupported);
			
			if(bIsAuto)
				p_MyCheck[i].SetCheck(BST_CHECKED);
			else
				p_MyCheck[i].SetCheck(BST_UNCHECKED);

		/*	if(pCap->ControlType == ASI_EXPOSURE)
			{
				//combobox
				rc.left = rc.right + 5;
				rc.right = rc.left + 50;
				rc.top = rc_client.top;
				rc.bottom = rc.top + 25;

				rc.MoveToY(p_CtrlOriyStart[i]);
				WD_Style	= 0x50010202;

				p_MyCombo = new CComboBox;
				p_MyCombo->Create(WD_Style , rc, this, iCountID++ );
				p_MyCombo->Clear();
				p_MyCombo->ResetContent();
				p_MyCombo->AddString("us");
				p_MyCombo->AddString("ms");
				p_MyCombo->AddString("s");
				p_MyCombo->SetCurSel(0);
			}*/
			
		

		}

		iCtrlYEnd = rc.bottom + 20;
		this->GetClientRect(&rc);
		
		if(iCtrlYEnd > rc.Height())
		{
			SCROLLINFO si = {0};
			si.cbSize = sizeof(si);
			si.fMask = SIF_RANGE|SIF_PAGE;
			si.nMin = 0;
			si.nMax = iCtrlYEnd;
			si.nPage = rc.Height();
			m_scrollV_ctrl.SetScrollInfo(&si);
			m_scrollV_ctrl.ShowWindow(SW_SHOW);
			
			m_scrollV_ctrl.SetScrollPos(0);

		}
		else
			m_scrollV_ctrl.ShowWindow(SW_HIDE);


	}
	else
	{
		m_scrollV_ctrl.ShowWindow(SW_HIDE);
		DeleteAllCtrl();
	}

	bCtrlDrawDone = true;
}



void CCtrlPanel::OnShowWindow(BOOL bShow, UINT nStatus)
{
	CStatic::OnShowWindow(bShow, nStatus);
	GetClientRect(&rc_slider);
	rc_client = rc_slider;
	rc_slider.left = rc_slider.right - 20;
	m_scrollV_ctrl.Create(0x50811080 | SBS_VERT, rc_slider, this, 2001);
	m_scrollV_ctrl.ShowWindow(SW_HIDE);
}
void CCtrlPanel::RefreshSliderEdit(int iCtrlIndex, int CtrlType, long lVlaue)
{
	CString cs;
	if(CtrlType == ASI_TEMPERATURE)
	{
		p_MySlider[iCtrlIndex].SetPos(lVlaue/10.0);
		cs.Format("%.1f",lVlaue/10.0);
		fSensorTempe = lVlaue/10.0;
		p_MyEdit[iCtrlIndex].SetWindowText(cs);
	}
	else if(CtrlType == ASI_EXPOSURE)
	{
		p_MySlider[iCtrlIndex].SetPos(lVlaue);
		cs.Format("%dms",lVlaue);
		p_MyEdit[iCtrlIndex].SetWindowText(cs);
	}
	else
	{
		p_MySlider[iCtrlIndex].SetPos(lVlaue);
		cs.Format("%d",lVlaue);
		p_MyEdit[iCtrlIndex].SetWindowText(cs);
	}
}
void CCtrlPanel::EnableControl(int iControlIndex, bool bAuto, bool bWriteable)
{

	if(bWriteable)//bAuto||!
	{
		pRefreshedIndex[iControlIndex] = bAuto;
		p_MySlider[iControlIndex].EnableWindow(!bAuto);
		p_MyEdit[iControlIndex].EnableWindow(!bAuto);

	}
	else
	{
		pRefreshedIndex[iControlIndex] = true;//不可写, 一定自动刷新
		p_MySlider[iControlIndex].EnableWindow(false);
		p_MyEdit[iControlIndex].EnableWindow(false);
	}


}
BOOL CCtrlPanel::PreTranslateMessage(MSG* pMsg)
{
	int i;
	if(bShowCtrl)
	{

		for(i = 0;i < iNumOfCtrl; i++)
		{
			if (pMsg->hwnd == p_MySlider[i].m_hWnd)	
			{
				if(pMsg->message == WM_LBUTTONUP)
					ControlSetValue(i);


			}
			else if(pMsg->hwnd == p_MyCheck[i].m_hWnd)
			{
				if(pMsg->message == WM_LBUTTONUP)
				{
					
					SetAuto(i, !p_MyCheck[i].GetCheck());
				}
			}
		}

		if(pMsg->message == WM_KEYDOWN)
		{
			switch(pMsg->wParam) 
			{
			case VK_RETURN: 				
				EditSetValue();				
				return TRUE;

			}
		}


		m_ToolTip.RelayEvent(pMsg);
	}

	return CStatic::PreTranslateMessage(pMsg);
}

void CCtrlPanel::ControlSetValue(int iControlIndex)
{
	long lControlValue;
	CString cs;
	lControlValue = p_MySlider[iControlIndex].GetPos();
	ASI_CONTROL_TYPE iControlType =  pControlCaps[iControlIndex]->ControlType;
	if(iControlType == ASI_EXPOSURE)
		lControlValue*=1000;
	ASI_ERROR_CODE err;
	ASI_BOOL bAuto = ASI_FALSE;
	err = ASISetControlValue(iCamIndex, iControlType, lControlValue, p_MyCheck[iControlIndex].GetCheck()? ASI_TRUE:ASI_FALSE);

	RefreshAllValue(iControlIndex);	

}

void CCtrlPanel::Value2Edit(long lValue, int iControlIndex, ASI_CONTROL_TYPE iControlType)
{
	CString cs;
	if(iControlType == ASI_EXPOSURE)
		cs.Format("%dms", lValue);
	else
		cs.Format("%d", lValue);
	p_MyEdit[iControlIndex].SetWindowText(cs);
}

long CSToLong(CString cs)
{
	char Char;
	long lNum = 0, lPow = 1;
	int i;
	for(i = cs.GetLength() - 1; i>=0; i--)
	{
		Char=cs.GetAt(i);
		lNum += (Char - '0')*lPow;
		lPow*=10;
	}
	return lNum;
}
void CCtrlPanel::EditSetValue()
{
	ASI_CONTROL_TYPE iControlType;
	for(int i = 0;i < iNumOfCtrl; i++)
	{
		iControlType = pControlCaps[i]->ControlType;
		if((CWnd*)&p_MyEdit[i] == GetFocus())
		{
			CString str;
			p_MyEdit[i].GetWindowText(str);
			LONGLONG lValue;
			lValue = atol(str);
			if(iControlType == ASI_EXPOSURE)
				lValue*=1000;
			ASISetControlValue(iCamIndex, iControlType, lValue, ASI_FALSE);

			RefreshAllValue(i);	
		}
	}

}

void CCtrlPanel::SetAuto(int iControlIndex, bool bChecked)
{
	long lValue = 0;
	ASI_CONTROL_TYPE iControlType;
	ASI_BOOL bAuto = ASI_FALSE;

	iControlType = pControlCaps[iControlIndex]->ControlType;
	ASIGetControlValue(iCamIndex, iControlType, &lValue, &bAuto);

	ASISetControlValue(iCamIndex, iControlType, lValue, (ASI_BOOL)bChecked );
	RefreshAllValue(iControlIndex);
}

void CCtrlPanel::OnVScroll(UINT nSBCode, UINT nPos, CScrollBar* pScrollBar)
{
	int PosMin, PosMax;
	m_scrollV_ctrl.GetScrollRange(&PosMin, &PosMax);
	int TempPos = pScrollBar->GetScrollPos();
	switch(nSBCode)
	{
	case SB_THUMBPOSITION://release 
		pScrollBar->SetScrollPos(nPos);
		MoveCtrl(nPos);
		break;
	case SB_LINEUP:
		if(TempPos > PosMin)
		{
			TempPos--;
		}
		pScrollBar->SetScrollPos(TempPos);
		MoveCtrl(TempPos);
		break;
	case SB_LINEDOWN:
		if(TempPos<PosMax)
		{
			TempPos++;
		}
		pScrollBar->SetScrollPos(TempPos);
		MoveCtrl(TempPos);
		break;
	case SB_PAGEDOWN:
		if(TempPos<PosMax)
		{
			TempPos++;
		}
		pScrollBar->SetScrollPos(TempPos);
		MoveCtrl(TempPos);
		break;
	case SB_PAGEUP:
		if(TempPos>PosMin)
		{
			TempPos--;
		}
		pScrollBar->SetScrollPos(TempPos);
		MoveCtrl(TempPos);
		break;
	}
	CStatic::OnVScroll(nSBCode, nPos, pScrollBar);
}
void CCtrlPanel::MoveCtrl(int deltaY)
{
	int i;
	CRect rc;
	for(i = 0; i < iNumOfCtrl; i++)
	{
		p_MyStatic[i].GetWindowRect(&rc);
		ScreenToClient(rc);
		rc.MoveToY(p_CtrlOriyStart[i] - deltaY);
		p_MyStatic[i].MoveWindow(rc);

		p_MySlider[i].GetWindowRect(&rc);
		ScreenToClient(rc);
		rc.MoveToY(p_CtrlOriyStart[i] - deltaY);
		p_MySlider[i].MoveWindow(rc);

		p_MyEdit[i].GetWindowRect(&rc);
		ScreenToClient(rc);
		rc.MoveToY(p_CtrlOriyStart[i] - deltaY);
		p_MyEdit[i].MoveWindow(rc);

		p_MyCheck[i].GetWindowRect(&rc);
		ScreenToClient(rc);
		rc.MoveToY(p_CtrlOriyStart[i] - deltaY);
		p_MyCheck[i].MoveWindow(rc);
	}
	::SendMessage(GetParent()->m_hWnd, WM_MY_MSG, WM_UPDATEUISTATE, IDC_STATIC_CTRLPANEL);//farther window refresh
}
void CCtrlPanel::OnTimer(UINT_PTR nIDEvent)
{
	if(nIDEvent == 1&&iCamIndex>-1&&bShowCtrl && bCtrlDrawDone)
	{
		int i ,iSliderID = 0, iEditID = 0, iAutoID = 0, iStaticID = 0;
		long lValue = 0;
		int bAuto = 0;
		CString cs;
		for(i = 0; i < iNumOfCtrl; i++)
		{
			if(pRefreshedIndex[i])
			{
				ASIGetControlValue(iCamIndex, pControlCaps[i]->ControlType, &lValue, (ASI_BOOL*)&bAuto);
				if(pControlCaps[i]->ControlType == ASI_EXPOSURE)
					lValue/=1000;
				RefreshSliderEdit(i, pControlCaps[i]->ControlType, lValue);				
			}
		}
	}
	CStatic::OnTimer(nIDEvent);
}

int CCtrlPanel::OnCreate(LPCREATESTRUCT lpCreateStruct)
{
	if (CStatic::OnCreate(lpCreateStruct) == -1)
		return -1;
	SetTimer(1, 1000, NULL);
	return 0;
}
void CCtrlPanel::RefreshValue(int iControlIndex, int iControlIndex0)//刷新实际的值到滑动条和编辑框
{
	if(bShowCtrl)
	{
		ASI_CONTROL_CAPS* pCap;
		long lControlValue;
		ASI_BOOL bIsAuto = ASI_FALSE;

		pCap = pControlCaps[iControlIndex];
		ASIGetControlValue(iCamIndex, pCap->ControlType, &lControlValue, &bIsAuto);//ControlType 而不是ControlIndex
		if(pCap->ControlType == ASI_EXPOSURE)
			lControlValue/=1000;
		RefreshSliderEdit(iControlIndex, pCap->ControlType, lControlValue);
		if(iControlIndex0 != iControlIndex)
		{
			if(bIsAuto)//导致pretranslatemessage里checkbox状态不能改变
				p_MyCheck[iControlIndex].SetCheck(BST_CHECKED);
			else
				p_MyCheck[iControlIndex].SetCheck(BST_UNCHECKED);
		}
		EnableControl(iControlIndex, bIsAuto, pCap->IsWritable == ASI_TRUE?true:false);
	}

}
void CCtrlPanel::RefreshAllValue(int iControlIndex0)
{
	if(bShowCtrl)
	{
		for(int i = 0;i < iNumOfCtrl; i++)
		{
			RefreshValue(i, iControlIndex0);
		}
	}
}

