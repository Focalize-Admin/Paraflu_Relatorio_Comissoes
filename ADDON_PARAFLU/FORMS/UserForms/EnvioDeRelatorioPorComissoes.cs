﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADDON_PARAFLU.DIAPI.Interfaces;
using ADDON_PARAFLU.servicos.Interfaces;
using ADDON_PARAFLU.FORMS.Recursos;
using Microsoft.Extensions.DependencyInjection;
using SAPbouiCOM;
using System.Globalization;
using ADDON_PARAFLU.Uteis.Interfaces;
using SAPbobsCOM;
using ADDON_PARAFLU.Services;
using ADDON_PARAFLU.Uteis;

namespace ADDON_PARAFLU.FORMS.UserForms
{
    public sealed class EnvioDeRelatorioPorComissoes
    {
        private readonly string formid;
        private readonly SAPbouiCOM.Form form;
        private readonly IAPI _api;
        private readonly IEmail _email;
        private readonly IPDFs _pdfs;
        private DataTable table;
        private double totalValue = 0;
        private Dictionary<string, Vendedores> vendedores = new Dictionary<string, Vendedores>();


        public EnvioDeRelatorioPorComissoes(IAPI api, IEmail email, IPDFs pdfs)
        {
            _api = api;
            _email = email;
            _pdfs = pdfs;
            _email.GetParamEmail();
            FormCreationParams cp = null;
            string xmlFormCode = Recursos.Recursos.EnvioComissões.ToString();
            try
            {
                cp = ((FormCreationParams)(SAPbouiCOM.Framework.Application.SBO_Application.CreateObject(BoCreatableObjectType.cot_FormCreationParams)));
                cp.FormType = "EnvioComissoes_Form";
                cp.XmlData = xmlFormCode;
                cp.UniqueID = "EnvioComissoes_Form";
                this.form = SAPbouiCOM.Framework.Application.SBO_Application.Forms.AddEx(cp);
                this.formid = this.form.UniqueID;
                CustomInitialize();
                table = form.DataSources.DataTables.Item("DT_0");
            }
            catch (Exception ex)
            {
                SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText($"Erro ao crair o formulário: {ex.Message}", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Error);
            }
            finally
            {
                if (form != null)
                {
                    form.Visible = true;
                }
            }
        }
        private void CustomInitialize()
        {
            SAPbouiCOM.Framework.Application.SBO_Application.ItemEvent += SBO_Application_ItemEvent;
        }
        private void SBO_Application_ItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;
            if (FormUID != formid)
                return;
            if (pVal.BeforeAction)
            {
                switch (pVal.EventType)
                {
                    case BoEventTypes.et_CLICK:
                        {
                            switch (pVal.ItemUID)
                            {
                                case "Item_1":
                                    {
                                        AtualizaGrid();
                                    }
                                    break;
                                case "Item_8":
                                    {
                                        EnviaEmails();
                                    }
                                    break;
                                case "Item_7":
                                    {
                                        MarcarDesmarcarTodos();
                                    }
                                    break;

                            }
                        }
                        break;
                }
            }
            else
            {
                switch (pVal.EventType)
                {
                    case BoEventTypes.et_CLICK:
                        {
                            if (pVal.ItemUID == "Item_6" && pVal.ColUID == "Selecionado" && !pVal.BeforeAction)
                            {
                                try
                                {
                                    form.Freeze(true);
                                    Grid grid = (Grid)form.Items.Item("Item_6").Specific;
                                    int row = grid.GetDataTableRowIndex(pVal.Row);
                                    DataTable dt = form.DataSources.DataTables.Item("DT_0");

                                    if (grid.DataTable.Columns.Item("Selecionado").Cells.Item(row).Value.ToString() == "Y")
                                    {
                                        string vendedor = grid.DataTable.Columns.Item("Código").Cells.Item(row).Value.ToString();
                                        string email = grid.DataTable.Columns.Item("Email do vendedor").Cells.Item(row).Value.ToString();
                                        string name = grid.DataTable.Columns.Item("Nome do Vendedor").Cells.Item(row).Value.ToString();

                                        string valor = dt.GetValue("Comissão", row).ToString();
                                        double val = 0;
                                        if (vendedores.TryGetValue(vendedor, out Vendedores value))
                                        {
                                            totalValue += Convert.ToDouble(dt.GetValue("Comissão", row).ToString(), new CultureInfo("pt-BR"));
                                            val = Math.Round(totalValue, 2);
                                            ((EditText)form.Items.Item("Item_11").Specific).Value = val.ToString(new CultureInfo("en-US"));
                                            return;
                                        }

                                        Vendedores rep = new Vendedores()
                                        {
                                            Code = vendedor,
                                            E_Mail = email,
                                            Name = name,
                                        };

                                        vendedores.Add(vendedor, rep);
                                        totalValue += Convert.ToDouble(dt.GetValue("Comissão", row).ToString(), new CultureInfo("pt-BR"));
                                        val = Math.Round(totalValue, 2);
                                        ((EditText)form.Items.Item("Item_11").Specific).Value = val.ToString(new CultureInfo("en-US"));
                                    }
                                    else
                                    {
                                        string vendedor = grid.DataTable.Columns.Item("Código").Cells.Item(row).Value.ToString();
                                        string email = grid.DataTable.Columns.Item("Email do vendedor").Cells.Item(row).Value.ToString();
                                        string name = grid.DataTable.Columns.Item("Nome do Vendedor").Cells.Item(row).Value.ToString();

                                        if (vendedores.TryGetValue(vendedor, out Vendedores value))
                                        {
                                            totalValue -= Convert.ToDouble(grid.DataTable.Columns.Item("Comissão").Cells.Item(row).Value.ToString(), new CultureInfo("pt-BR"));
                                            double val = Math.Round(totalValue, 2);
                                            ((EditText)form.Items.Item("Item_11").Specific).Value = val.ToString(new CultureInfo("en-US"));
                                            return;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText($"erro ao atualizar o valor de cotação {ex.Message}");
                                }
                                finally
                                {
                                    form.Freeze(false);
                                }
                            }
                        }
                        break;
                }
            }
        }

        private void MarcarDesmarcarTodos()
        {
            bool marcado = !((SAPbouiCOM.CheckBox)form.Items.Item("Item_7").Specific).Checked;
            MarcarTodos(marcado);
        }

        private void MarcarTodos(bool marcado)
        {
            form.Freeze(true);
            try
            {
                DataTable dt = form.DataSources.DataTables.Item("DT_0");
                string valor = marcado ? "Y" : "N";
                for (int row = 0; row < dt.Rows.Count; row++)
                {
                    dt.SetValue("Selecionado", row, valor);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                form.Freeze(false);
            }
        }

        private void AtualizaGrid()
        {
            form.Freeze(true);
            try
            {
                SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText("Pesquisando dados...", BoMessageTime.bmt_Medium, BoStatusBarMessageType.smt_Warning);
                DataTable dt = form.DataSources.DataTables.Item("DT_0");
                Grid grid = (Grid)form.Items.Item("Item_6").Specific;
                string dataini = ((EditText)this.form.Items.Item("Item_3").Specific).Value;
                string datafim = ((EditText)this.form.Items.Item("Item_4").Specific).Value;
                dataini = dataini.Substring(0, 4) + "/" + dataini.Substring(4, 2) + "/" + dataini.Substring(6, 2);
                datafim = datafim.Substring(0, 4) + "/" + datafim.Substring(4, 2) + "/" + datafim.Substring(6, 2);
                string query = "";
                if (string.IsNullOrEmpty(dataini))
                {
                    SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText("Selecione um perido inicial", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
                    return;
                }
                if (string.IsNullOrEmpty(datafim))
                {
                    SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText("Selecione um perido final", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Warning);
                    return;
                }
                if (_api.Company.DbServerType != BoDataServerTypes.dst_HANADB)
                    query = Queries.Notas_Fiscais_SQL.Replace("Dataini", dataini).Replace("Datafim", datafim);
                else
                    query = Queries.Notas_Fiscais_HANA.Replace("Dataini", dataini).Replace("Datafim", datafim);
                dt.ExecuteQuery(query);
                grid.Columns.Item("Selecionado").Type = BoGridColumnType.gct_CheckBox;
                for (int index = 0; index < grid.Columns.Count; index++)
                    grid.Columns.Item(index).Editable = false;
                grid.Columns.Item("Selecionado").Editable = true;
            }
            catch (Exception ex)
            {
                SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText($"Erro ao buscar os dados: {ex.Message}");
            }
            finally
            {
                SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText("Dados encontrados...", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Success);
                form.Freeze(false);
            }
        }
        private (string user, string senha, string past) GetDataForBD()
        {
            Recordset recordset = (Recordset)_api.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = @"SELECT ""U_User"", ""U_Pass"", ""U_Past"" FROM ""@FOC_DB_CONF"" WHERE ""Code"" = '1'";
            recordset.DoQuery(query);
            if (recordset.RecordCount > 0)
            {
                return (Security.Decrypt(recordset.Fields.Item(0).Value.ToString()), Security.Decrypt(recordset.Fields.Item(1).Value.ToString()), recordset.Fields.Item(2).Value.ToString());
            }
            return (string.Empty, string.Empty, string.Empty);
        }
        private void EnviaEmails()
        {
            form.Freeze(true);
            Recordset recordset = (Recordset)_api.Company.GetBusinessObject(BoObjectTypes.BoRecordset);
            string query = @"SELECT ""U_Body"" FROM ""@FOC_EMAIL_PARAM"" WHERE ""Code"" = '1'";
            recordset.DoQuery(query);
            DataTable dt = form.DataSources.DataTables.Item("DT_0");
            (string user, string senha, string past) = GetDataForBD();
            try
            {
                form.Freeze(true);
                Vendedores[] values = vendedores.Values.ToArray();
                for (int index = 0; index < values.Length; index++)
                {
                    Vendedores vendedores = values[index];
                    string body = recordset.Fields.Item("U_Body").Value.ToString();
                    string reportPath = @$"{System.Windows.Forms.Application.StartupPath}ReportComissões.rpt";
                    string caminho = "";
                    string cardCode = vendedores.Code;
                    string slpName = vendedores.Name;
                    string periodo2 = ((EditText)this.form.Items.Item("Item_4").Specific).Value;
                    string periodo1 = ((EditText)this.form.Items.Item("Item_3").Specific).Value;
                    periodo1 = periodo1.Substring(0, 4) + "-" + periodo1.Substring(4, 2) + "-" + periodo1.Substring(6, 2);
                    periodo2 = periodo2.Substring(0, 4) + "-" + periodo2.Substring(4, 2) + "-" + periodo2.Substring(6, 2);
                    if(string.IsNullOrEmpty(caminho))
                    caminho = $"{past}\\{slpName}_{periodo1}_{periodo2}.pdf";
                    string caminhoPdf = _pdfs.GeraPDF(periodo1, periodo2, cardCode, user, senha, reportPath, caminho);
                    SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText("Enviando Email...", BoMessageTime.bmt_Medium, BoStatusBarMessageType.smt_Warning);
                    string[] anexos = new string[] { caminhoPdf };
                    _email.EnviarPorEmail(vendedores.E_Mail.Split('@').First(), vendedores.E_Mail, anexos, body);
                }
            }
            catch (Exception ex)
            {
                SAPbouiCOM.Framework.Application.SBO_Application.MessageBox($"Erro ao enviar email:{ex.Message}");
            }
            finally
            {
                SAPbouiCOM.Framework.Application.SBO_Application.StatusBar.SetText("Email enviado com sucesso!", BoMessageTime.bmt_Short, BoStatusBarMessageType.smt_Success);
                form.Freeze(false);
            }
        }
    }
}
