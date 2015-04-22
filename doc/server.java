package serve_weka;

import weka.classifiers.Classifier;
import weka.core.Attribute;
import weka.core.FastVector;
import weka.core.Instance;
import weka.core.Instances;

import java.io.BufferedReader;
import java.io.FileInputStream;
import java.io.FileReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.ObjectInputStream;
import java.io.PrintWriter;
import java.net.ServerSocket;
import java.net.Socket;

public class server extends Thread
{ 
 protected Socket clientSocket;
 static Classifier cls;
 static Instances inst;
 static int Port = 27017;
 
 public static void main(String[] args) throws Exception 
   { 
	 ServerSocket serverSocket = null; 
	try {
		cls = (Classifier) weka.core.SerializationHelper.read("C:/Users/datalab/Desktop/SVM Server/glasses.model");
		inst = new Instances(
		         new BufferedReader(
		           new FileReader("C:/Users/datalab/Desktop/SVM Server/outfile.arff")));
		inst.setClassIndex(inst.numAttributes() - 1);
	} catch (Exception e1) {
		// TODO Auto-generated catch block
		e1.printStackTrace();
	}

    try { 
         serverSocket = new ServerSocket(Port); 
         System.out.println ("Connection Socket Created");
         try { 
              while (true)
                 {
                  //System.out.println ("Waiting for Connection");
                  new server (serverSocket.accept()); 
                 }
             } 
         catch (IOException e) 
             { 
              System.err.println("Accept failed."); 
              System.exit(1); 
             } 
        } 
    catch (IOException e) 
        { 
         System.err.println("Could not listen on port: "+Integer.toString(Port)); 
         System.exit(1); 
        } 
    finally
        {
         try {
              serverSocket.close(); 
             }
         catch (IOException e)
             { 
              System.err.println("Could not close port: 10008."); 
              System.exit(1); 
             } 
        }
   }

 private server (Socket clientSoc)
   {
    clientSocket = clientSoc;
    start();
   }

 public void run()
   {
    //System.out.println ("New Communication Thread Started");

    try { 
         PrintWriter out = new PrintWriter(clientSocket.getOutputStream(), 
                                      true); 
         BufferedReader in = new BufferedReader( 
                 new InputStreamReader( clientSocket.getInputStream())); 

         String inputLine; 

         while ((inputLine = in.readLine()) != null) 
             { 
              String[] terms = inputLine.split(",");
              Instance instance = new Instance(terms.length); 
              instance.setDataset(inst);
              //System.out.println(inputLine);
              for (int i =0; i < inst.numAttributes() - 1;i++)
              {
            	  instance.setValue(i, Double.parseDouble(terms[i]));
              }
              double value=cls.classifyInstance(instance);
              //System.out.println(value);

            //get the name of the class value
              if (value>.5)
              {
            	  out.println("Yes\0");
              }
              else
              {
            	  out.println("No\0");
              }
              
             } 

         out.close(); 
         in.close(); 
         clientSocket.close(); 
        } 
    catch (IOException e) 
        { 
         System.err.println("Problem with Communication Server");
         System.exit(1); 
        } catch (Exception e) {
		// TODO Auto-generated catch block
		e.printStackTrace();
		System.exit(1);
	} 
    }
} 